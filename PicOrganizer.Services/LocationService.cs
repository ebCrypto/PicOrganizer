using CoordinateSharp;
using CsvHelper;
using ExifLibrary;
using Microsoft.Extensions.Logging;
using Models;
using PicOrganizer.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using static PicOrganizer.Services.ILocationService;

namespace PicOrganizer.Services
{
    public class LocationService : ILocationService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<LocationService> logger;
        private readonly IReportReadWriteService reportService;
        private readonly IFileProviderService fileProviderService;
        private readonly IDateRecognizerService dateRecognizerService;
        private readonly ParallelOptions parallelOptions;
        private Dictionary<LocationWriter, ConcurrentDictionary<string, List<ReportDetail>>> lastLocationDetailRun;
        private List<ReportMissingLocation> timeline;
        private List<Location> knownLocations;

        public List<ReportMissingLocation> GetTimeline()
        {
            return timeline;
        }

        public LocationService(AppSettings appSettings, ILogger<LocationService> logger, IReportReadWriteService reportService, IFileProviderService fileProviderService, IDateRecognizerService dateRecognizerService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.reportService = reportService;
            this.fileProviderService = fileProviderService;
            this.dateRecognizerService = dateRecognizerService;
            this.parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = appSettings.MaxDop };
            lastLocationDetailRun = new Dictionary<LocationWriter, ConcurrentDictionary<string, List<ReportDetail>>>();
        }

        public IEnumerable<ReportDetail> ReportMissing(DirectoryInfo di, LocationWriter lw, bool writeToDisk = true)
        {
            if (di.FullName == appSettings.OutputSettings.TargetDirectories.First())
                lastLocationDetailRun.Add(lw, new ConcurrentDictionary<string, List<ReportDetail>>());
            logger.LogDebug("About to create {Type} Location Report in {Directory}",lw, di.FullName);
            var topPictures = fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.TopDirectoryOnly, lw == LocationWriter.FromClosestSameDay);
            var topReportDetails = topPictures.Select(f => GetReportDetail(f)).ToList() ?? new List<ReportDetail>();
            if (topReportDetails.Any())
                lastLocationDetailRun[lw].AddOrUpdate(di.FullName, topReportDetails, (k,v)=> v);
            if(writeToDisk)
            {
                var reportDetail = new FileInfo(Path.Combine(appSettings.OutputSettings.TargetDirectories.First(), appSettings.OutputSettings.ReportsFolderName, string.Format("{0}_{1}_{2}", di.Name, lw, appSettings.OutputSettings.ReportDetailName)));
                if (!reportDetail.Directory.Exists)
                    reportDetail.Directory.Create();
                reportService.Write(reportDetail, topReportDetails.OrderBy(p => p.DateTime).ToList());
            }

            var filesInSubfolders = di.GetDirectories().Select(d => ReportMissing(d,lw,writeToDisk)).ToList();
            var subLevelReport = filesInSubfolders.SelectMany(p => p).ToList();
            var comboReport = topReportDetails.Union(subLevelReport);

            if (writeToDisk)
            {
                var reportMissingLoc = new FileInfo(Path.Combine(appSettings.OutputSettings.TargetDirectories.First(), appSettings.OutputSettings.ReportsFolderName, string.Format("{0}_{1}_{2}", di.Name, lw, appSettings.OutputSettings.ReportMissingLocName)));
                reportService.Write(reportMissingLoc, ComputeMissingLocations(comboReport)); 
            }
            return comboReport;
        }

        private static List<ReportMissingLocation> ComputeMissingLocations(IEnumerable<ReportDetail> files)
        {
            var dates = files.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)).Select(p => p.DateTime.Date).Distinct();
            return dates.OrderBy(p => p).Select(p => new ReportMissingLocation() { Start = p, End = p.AddDays(1).AddSeconds(-1) }).ToList();
        }

        private ReportDetail GetReportDetail(FileInfo fileInfo)
        {
            var r = new ReportDetail()
            {
                FullFileName = fileInfo.FullName,
            };
            if (fileInfo == null || fileInfo.Directory?.Name == appSettings.OutputSettings.InvalidJpegFolderName)
                return r;
            try
            {
                ImageFile imageFile;
                ExifProperty da;
                DateTime dt = DateTime.MinValue;
                GPSLatitudeLongitude latTag;
                GPSLatitudeLongitude longTag;
                try
                {
                    imageFile = ImageFile.FromFile(fileInfo.FullName);
                    da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(da?.ToString(), out dt);
                    latTag = imageFile.Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude);
                    longTag = imageFile.Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLongitude);
                    r.DateTime = dt;
                    if (!IsNullOrZero(latTag) && !IsNullOrZero(longTag)) // very unlikely for a location to be 0.0;0.0 as it is in the Atlantic 
                    {
                        r.Latitude = latTag?.ToString();
                        r.Longitude = longTag?.ToString();
                    }
                }
                catch (NotValidJPEGFileException e)
                {
                    logger.LogWarning("{File} invalid JPEG {Message}", fileInfo.FullName, e.Message);
                }
                catch (NotValidImageFileException e)
                {
                    logger.LogWarning("{File} invalid image {Message}", fileInfo.FullName, e.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            } 
            return r;
        }

        private static bool IsNullOrZero(GPSLatitudeLongitude latTag)
        {
            if (latTag == null)
                return true;
            if (latTag.Degrees.Numerator == 0 && latTag.Minutes.Numerator == 0 && latTag.Seconds.Numerator == 0)
                return true;
            return false;
        }

        public async Task WriteLocation(DirectoryInfo di, LocationWriter lw)
        {
            logger.LogInformation(@"About to Write Location {Type} for pictures in {Source}...",lw, di.FullName);
            switch (lw)
            {
                case LocationWriter.FromClosestSameDay:
                    {
                        if (!lastLocationDetailRun.ContainsKey(lw))
                        {
                            ReportMissing(di,lw, false);
                        }

                        if (lastLocationDetailRun.ContainsKey(lw))
                            foreach (var report in lastLocationDetailRun[lw])
                                await WriteLocationFromClosestKnownIfSameDay(report);
                        //Parallel.ForEach(lastLocationDetailRun[lw], parallelOptions, async report => await WriteLocationFromClosestKnownIfSameDay(report.Value));
                        else
                            logger.LogWarning("Unable to report missing locations.");
                        break;
                    }
                case LocationWriter.FromFileName:
                    {
                        if (!lastLocationDetailRun.ContainsKey(lw))
                        {
                            ReportMissing(di, lw, false);
                        }

                        if (lastLocationDetailRun.ContainsKey(lw))
                            foreach (var report in lastLocationDetailRun[lw])
                                await WriteLocationFromFileName(report);
                        //Parallel.ForEach(lastLocationDetailRun[lw], parallelOptions, async report => await WriteLocationFromFileName(report));
                        else
                            logger.LogWarning("Unable to report missing locations.");
                        break;
                    }
                default:
                    {
                        if (!lastLocationDetailRun.ContainsKey(lw))
                        {
                            ReportMissing(di, lw, false);
                        }
                        if (lastLocationDetailRun.ContainsKey(lw))
                            foreach (var report in lastLocationDetailRun[lw])
                                await AddlocationFromTimeLine(report);
                        break;
                    }
            }
        }

        private async Task AddlocationFromTimeLine(KeyValuePair<string, List<ReportDetail>> dic)
        {
            if (dic.Key == appSettings.OutputSettings.InvalidJpegFolderName || dic.Key == appSettings.OutputSettings.UnknownDateFolderName)
                return;
            int count = 0;
            foreach (var picture in dic.Value.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)))
            {
                var result = await AddlocationFromTimeLine(new FileInfo(picture.FullFileName));
                if (result)
                    count++;
            }
            logger.LogInformation("Added locations to {Count} files in {Directory} using TimeLine", count, dic.Key);
        }

        private async Task<bool> AddlocationFromTimeLine(FileInfo fi)
        {
            try
            {
                if (fi == null || fi.Directory.Name == appSettings.OutputSettings.InvalidJpegFolderName || fi.Directory.Name == appSettings.OutputSettings.UnknownDateFolderName)
                    return false;
                var imageFile = await ImageFile.FromFileAsync(fi.FullName);
                var da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                _ = DateTime.TryParse(da?.ToString(), out var dt);

                if (!dateRecognizerService.Valid(dt))
                    return false;

                var result = GetTimeline().Where(p => p.Start <= dt && p.End >= dt && !string.IsNullOrEmpty(p.Latitude) && !string.IsNullOrEmpty(p.Longitude));
                if (result == null || !result.Any())
                {
                    logger.LogWarning("No location found on {Date} using the provided timeline", dt.ToString());
                    return false;
                }
                if (result.Count() > 1)
                {
                    logger.LogWarning("Multiple locations ({Count}) found on {Date} using the provided timeline", result.Count(), dt.ToString());
                    return false;
                }
                var first = result.First();
                var latitude = first.Latitude;
                var longitude = first.Longitude;
                await SaveCoordinatesToImage(latitude, longitude, fi);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add coordinates to {File}", fi.FullName);
                return false;
            }
        }

        private async Task WriteLocationFromFileName(KeyValuePair<string, List<ReportDetail>> dic)
        {
            if(dic.Key.Contains(appSettings.OutputSettings.WhatsappFolderName,StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug("not applyting timeline locations to {Directory}", dic.Key);
                return;
            }
            int count = 0;
            foreach (var picture in dic.Value.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)))
            {
                foreach (var knownLocation in knownLocations)
                {
                    if (picture.FullFileName.Contains(knownLocation.NameInFile))
                    {
                        logger.LogTrace("Will write location ({Latitude} {Longitude}) from {From} to {To}", knownLocation.Latitude, knownLocation.Longitude, knownLocation.ActualLocation, picture.FullFileName);
                        double.TryParse(knownLocation.Latitude, out var lat);
                        double.TryParse(knownLocation.Longitude, out var lon);
                        await SaveCoordinatesToImage(lat, lon, new FileInfo(picture.FullFileName));
                        count++;
                    }
                } 
            }
            logger.LogInformation("Added locations to {Count} files in {Directory} using FileName", count, dic.Key);
        }

        private async Task WriteLocationFromClosestKnownIfSameDay(KeyValuePair<string, List<ReportDetail>> dic)
        {
            int count = 0;
            var reportDetails = dic.Value;
            foreach (var picture in reportDetails.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)))
            {
                var picKnownLoc = reportDetails
                    .Where(p =>
                            !string.IsNullOrEmpty(p.Latitude) && !string.IsNullOrEmpty(p.Longitude) && picture.DateTime.Date == p.DateTime.Date
                            )
                    .OrderBy(p =>
                            Math.Abs(p.DateTime.Ticks - picture.DateTime.Ticks))
                    .FirstOrDefault();

                if (picKnownLoc != null)
                {
                    logger.LogTrace("Will copy location ({Latitude} {Longitude}) from {From} to {To}", picKnownLoc.Latitude, picKnownLoc.Longitude, picKnownLoc.FullFileName, picture.FullFileName);
                    await SaveDMSCoordinatesToImage(picKnownLoc.Latitude, picKnownLoc.Longitude, new FileInfo(picture.FullFileName));
                    count++;
                }
                else
                    logger.LogDebug("Unable to find a picture with location taken on {Date}", picture.DateTime.Date.ToShortDateString());
            }
            logger.LogInformation("Added locations to {Count} files in {Directory} using SameDayKnownLocation", count, dic.Key);
        }

        public async Task SaveCoordinatesToImage(string latitude, string longitude, FileInfo fi)
        {
            await SaveCoordinatesToImage(Convert.ToDouble(latitude), Convert.ToDouble(longitude), fi);
        }

        public async Task SaveCoordinatesToImage(double latitude, double longitude, FileInfo fi)
        {
            if (fi == null || fi.Directory == null || fi.Directory.Name == appSettings.OutputSettings.InvalidJpegFolderName)
                return;
            try
            {
                var ef = await ImageFile.FromFileAsync(fi.FullName);
                var c = new Coordinate(latitude, longitude, DateTime.Today);
                MakeLatitude(c, ef);
                MakeLongitude(c, ef);
                await ef.SaveAsync(fi.FullName);
                logger.LogTrace("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
                logger.LogDebug("Added {Coordinates} to {File}", c, fi.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add Float coordinates to {File}", fi.FullName);
            }
        }

        public async Task SaveDMSCoordinatesToImage(string latitude, string longitude, FileInfo fi)
        {
            try
            {
                var ef = await ImageFile.FromFileAsync(fi.FullName);
                MakeLatitude(latitude, ef);
                MakeLongitude(longitude, ef);
                await ef.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add DMS coordinates to {File}", fi.FullName);
            }
        }

        public void MakeLongitude(Coordinate coordinate, ImageFile ef)
        {
            string lon = coordinate.Longitude.ToString();
            MakeLongitude(lon, ef);
        }

        private static void MakeLongitude(string lon, ImageFile ef)
        {
            if (string.IsNullOrEmpty(lon))
                return;
            var items = lon.Replace("°", " ").Replace("º", " ").Replace("'", " ").Replace("\"", " ").Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (lon.StartsWith("W") || lon.StartsWith("E"))
            {
                ef.Properties.Set(ExifTag.GPSLongitude, GetFloat(items[1]), GetFloat(items[2]), GetFloat(items[3]));
                ef.Properties.Set(ExifTag.GPSLongitudeRef, items[0] == "E" ? GPSLongitudeRef.East : GPSLongitudeRef.West);
            }
            else
            {
                ef.Properties.Set(ExifTag.GPSLongitude, GetFloat(items[0]), GetFloat(items[1]), GetFloat(items[2]));
                ef.Properties.Set(ExifTag.GPSLongitudeRef, GetFloat(items[0]) < 0? GPSLongitudeRef.East : GPSLongitudeRef.West);
            }
        }

        public void MakeLatitude(Coordinate coordinate, ImageFile ef)
        {
            // N 55º 40' 35.883"
            string lat = coordinate.Latitude.ToString();
            MakeLatitude(lat,ef);
        }

        private static void MakeLatitude(string lat,ImageFile ef)
        {
            if (string.IsNullOrEmpty(lat))
                return;
            
            var items = lat.Replace("°", " ").Replace("º", " ").Replace("'", " ").Replace("\"", " ").Split(" ", StringSplitOptions.RemoveEmptyEntries);

            if (lat.StartsWith("N") || lat.StartsWith("S"))
            {
                ef.Properties.Set(ExifTag.GPSLatitude, GetFloat(items[1]), GetFloat(items[2]), GetFloat(items[3]));
                ef.Properties.Set(ExifTag.GPSLatitudeRef, items[0] == "N" ? GPSLatitudeRef.North : GPSLatitudeRef.South);
            }
            else
            {
                ef.Properties.Set(ExifTag.GPSLatitude, GetFloat(items[0]), GetFloat(items[1]), GetFloat(items[2]));
                ef.Properties.Set(ExifTag.GPSLatitudeRef, GetFloat(items[0]) > 0 ? GPSLatitudeRef.North : GPSLatitudeRef.South);
            }
        }

        private static float GetFloat(string s)
        {
            return Convert.ToSingle(s);
        }

        public void LoadTimeLine(FileInfo csv)
        {
            using var reader = new StreamReader(csv.FullName);
            using CsvReader? csvReader = new(reader, CultureInfo.InvariantCulture);
            timeline = csvReader.GetRecords<ReportMissingLocation>().ToList();
            VerifyTimeLine();
        }

        public void VerifyTimeLine()
        {
            DateTime? lastEnd = null;
            foreach (var time in GetTimeline())
            {
                if (lastEnd != null && time.Start != null && time.Start < lastEnd)
                    logger.LogError("Unexpected timeline element starting at {Start}, which is before {LastEnd}", time.Start.ToString(), lastEnd.ToString());
                lastEnd = time.End;
            }
            logger.LogDebug("Done verifying timeline");
        }

        public void LoadKnownLocations(FileInfo csv)
        {
            using var reader = new StreamReader(csv.FullName);
            using CsvReader? csvReader = new(reader, CultureInfo.InvariantCulture);
            knownLocations = csvReader.GetRecords<Location>().ToList();
        }
    }
}