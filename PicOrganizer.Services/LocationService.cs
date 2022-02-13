using CompactExifLib;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Models;
using PicOrganizer.Models;
using System.Collections.Concurrent;
using System.Globalization;
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
                //try
                //{
                    var imageFile = new ExifData(fileInfo.FullName);
                    imageFile.GetDateTaken(out DateTime dt);

                    imageFile.GetGpsLatitude(out var latTag);
                    imageFile.GetGpsLongitude(out var longTag);

                    r.DateTime = dt;
                    if (!IsZero(latTag) && !IsZero(longTag)) // very unlikely for a legit location to be 0.0;0.0 as it is in the Atlantic 
                    {
                        r.Latitude = PrintCoordinate(latTag);
                        r.Longitude = PrintCoordinate(longTag);
                    }
                //}
                //catch (NotValidJPEGFileException e)
                //{
                //    logger.LogWarning("{File} invalid JPEG {Message}", fileInfo.FullName, e.Message);
                //}
                //catch (NotValidImageFileException e)
                //{
                //    logger.LogWarning("{File} invalid image {Message}", fileInfo.FullName, e.Message);
                //}
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Can't GetReortDetail {FileName}", fileInfo.Name);
            } 
            return r;
        }

        private static string PrintCoordinate(GeoCoordinate c)
        {
            // N 55º 40' 35.883"
            return string.Format($"{c.CardinalPoint} {c.Degree}º {c.Minute}' {c.Second}\"");
        }

        private static bool IsZero(GeoCoordinate latTag)
        {
            return latTag.Degree == 0 && latTag.Minute == 0 && latTag.Second == 0;
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
                                WriteLocationFromClosestKnownIfSameDay(report);
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
                var imageFile = new ExifData (fi.FullName);
                imageFile.GetDateTaken(out DateTime dt);

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
                        await SaveCoordinatesToImage(knownLocation.Latitude, knownLocation.Longitude, new FileInfo(picture.FullFileName));
                        count++;
                    }
                } 
            }
            logger.LogInformation("Added locations to {Count} files in {Directory} using FileName", count, dic.Key);
        }

        private void WriteLocationFromClosestKnownIfSameDay(KeyValuePair<string, List<ReportDetail>> dic)
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
                    CopyGpsData(new FileInfo(picKnownLoc.FullFileName), new FileInfo(picture.FullFileName));
                    count++;
                }
                else
                    logger.LogDebug("Unable to find a picture with location taken on {Date}", picture.DateTime.Date.ToShortDateString());
            }
            logger.LogInformation("Added locations to {Count} files in {Directory} using SameDayKnownLocation", count, dic.Key);
        }

        private void CopyGpsData(FileInfo source, FileInfo target)
        {
            try
            {
                var src = new ExifData(source.FullName);
                var tgt = new ExifData(target.FullName);

                src.GetGpsLatitude(out var lat);
                src.GetGpsLongitude(out var lon);
                src.GetGpsAltitude(out var alt);

                tgt.SetGpsLatitude(lat);
                tgt.SetGpsLongitude(lon);
                tgt.SetGpsAltitude(alt);

                tgt.Save();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to copy GPS data from {Source} to {Target}", source, target);
            }
        }

        public async Task SaveCoordinatesToImage(string latitude, string longitude, FileInfo fi)
        {
            decimal.TryParse(latitude, out var lat);
            decimal.TryParse(longitude, out var lon);
            SaveCoordinatesToImage(lat, lon, fi);
        }

        public void SaveCoordinatesToImage(decimal latitude, decimal longitude, FileInfo fi)
        {
            if (fi == null || fi.Directory == null || fi.Directory.Name == appSettings.OutputSettings.InvalidJpegFolderName)
                return;
            try
            {
                var tgt = new ExifData(fi.FullName);

                tgt.SetGpsLatitude(GeoCoordinate.FromDecimal(latitude, true));
                tgt.SetGpsLongitude(GeoCoordinate.FromDecimal(longitude, false)); 

                tgt.Save();
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add Float coordinates to {File}", fi.FullName);
            }
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