using CoordinateSharp;
using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Collections.Concurrent;
using System.Linq;
using static PicOrganizer.Services.ILocationService;

namespace PicOrganizer.Services
{
    public class LocationService : ILocationService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<LocationService> logger;
        private readonly IReportReadWriteService reportService;
        private readonly ITimelineToFilesService timelineService;
        private readonly IFileProviderService fileProviderService;
        private readonly IDateRecognizerService dateRecognizerService;
        private readonly ParallelOptions parallelOptions;
        private ConcurrentDictionary<string, List<ReportDetail>> lastLocationDetailRun;

        public LocationService(AppSettings appSettings, ILogger<LocationService> logger, IReportReadWriteService reportService, ITimelineToFilesService timelineService, IFileProviderService fileProviderService, IDateRecognizerService dateRecognizerService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.reportService = reportService;
            this.timelineService = timelineService;
            this.fileProviderService = fileProviderService;
            this.dateRecognizerService = dateRecognizerService;
            this.parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = appSettings.MaxDop };
        }

        public IEnumerable<ReportDetail> ReportMissing(DirectoryInfo di, string step, bool writeToDisk = true)
        {
            if (di.FullName == appSettings.OutputSettings.TargetDirectories.First() )
                lastLocationDetailRun = new ConcurrentDictionary<string, List<ReportDetail>>();

            logger.LogDebug("About to create Location Report in {Directory}", di.FullName);
            var topPictures = fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.TopDirectoryOnly);
            var topReportDetails = topPictures.Select(f => GetReportDetail(f)).ToList() ?? new List<ReportDetail>();
            if (topReportDetails.Any())
                lastLocationDetailRun.AddOrUpdate(di.FullName, topReportDetails, (k,v)=> v);
            if(writeToDisk)
            {
                var reportDetail = new FileInfo(Path.Combine(appSettings.OutputSettings.TargetDirectories.First(), appSettings.OutputSettings.ReportsFolderName, string.Format("{0}_{1}_{2}", di.Name, step, appSettings.OutputSettings.ReportDetailName)));
                if (!reportDetail.Directory.Exists)
                    reportDetail.Directory.Create();
                reportService.Write(reportDetail, topReportDetails.OrderBy(p => p.DateTime).ToList());
            }

            var filesInSubfolders = di.GetDirectories().Select(d => ReportMissing(d,step,writeToDisk)).ToList();
            var subLevelReport = filesInSubfolders.SelectMany(p => p).ToList();
            var comboReport = topReportDetails.Union(subLevelReport);

            if (writeToDisk)
            {
                var reportMissingLoc = new FileInfo(Path.Combine(appSettings.OutputSettings.TargetDirectories.First(), appSettings.OutputSettings.ReportsFolderName, string.Format("{0}_{1}_{2}", di.Name, step, appSettings.OutputSettings.ReportMissingLocName)));
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
            if (lw == LocationWriter.FromClosestSameDay)
            {
                if (lastLocationDetailRun == null)
                {
                    logger.LogInformation("Creating data necessary to process missing locations. This might take a while... ");
                    ReportMissing(di, string.Empty, false);
                }

                if (lastLocationDetailRun != null)
                {
                    Parallel.ForEach(lastLocationDetailRun,parallelOptions, async report => await WriteLocationFromClosestKnownIfSameDay(report.Value));
                }
                else
                    logger.LogWarning("Unable to report missing locations.");
            }
            else
            {
                var topFiles = fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.AllDirectories);
                logger.LogDebug("Found {Count} files to add location from timeline to", topFiles.Count());
                await topFiles.ParallelForEachAsync(AddlocationFromTimeLine, appSettings.MaxDop);
            }
        }
        private async Task AddlocationFromTimeLine(FileInfo fi)
        {
            try
            {
                if (fi == null || fi.Directory.Name == appSettings.OutputSettings.InvalidJpegFolderName)
                    return;
                var imageFile = await ImageFile.FromFileAsync(fi.FullName);
                var da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                _ = DateTime.TryParse(da?.ToString(), out var dt);

                if (!dateRecognizerService.Valid(dt))
                    return;

                var result = timelineService.GetTimeline().Where(p => p.Start <= dt && p.End >= dt && !string.IsNullOrEmpty(p.Latitude) && !string.IsNullOrEmpty(p.Longitude));
                if (result == null || !result.Any())
                {
                    logger.LogWarning("No location found on {Date} using the provided timeline", dt.ToString());
                    return;
                }
                if (result.Count() > 1)
                {
                    logger.LogWarning("Multiple locations ({Count}) found on {Date} using the provided timeline", result.Count(), dt.ToString());
                    return;
                }
                var first = result.First();

                var latitude = first.Latitude;
                var longitude = first.Longitude;
                var c = new Coordinate(Convert.ToDouble(latitude), Convert.ToDouble(longitude), DateTime.Today);
                MakeLatitude(c, imageFile);
                MakeLongitude(c, imageFile);
                await imageFile.SaveAsync(fi.FullName);
                logger.LogTrace("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add coordinates to {File}", fi.FullName);
            }
        }

        private async Task WriteLocationFromClosestKnownIfSameDay(IEnumerable<ReportDetail> reportDetails)
        {
            int count = 0;
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
                    logger.LogDebug("Will copy location ({Latitude} {Longitude}) from {From} to {To}", picKnownLoc.Latitude, picKnownLoc.Longitude, picKnownLoc.FullFileName, picture.FullFileName);
                    await SaveDMSCoordinatesToImage(picKnownLoc.Latitude, picKnownLoc.Longitude, new FileInfo(picture.FullFileName));
                    count++;
                }
                else
                    logger.LogDebug("Unable to find a picture with location taken on {Date}", picture.DateTime.Date.ToShortDateString());
            }
            logger.LogInformation("Added locations to {Count} files", count);
        }

        public async Task SaveDoubleCoordinatesToImage(string latitude, string longitude, FileInfo fi)
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
    }
}