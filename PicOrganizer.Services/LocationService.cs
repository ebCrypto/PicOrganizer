using CoordinateSharp;
using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
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

        public LocationService(AppSettings appSettings, ILogger<LocationService> logger, IReportReadWriteService reportService, ITimelineToFilesService timelineService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.reportService = reportService;
            this.timelineService = timelineService;
        }

        public async Task<IEnumerable<ReportDetail>> ReportMissing(DirectoryInfo di, string step)
        {
            logger.LogDebug("About to create Location Report in {Directory}", di.FullName);
            var topFiles = di.GetFilesViaPattern(appSettings.PictureFilter, SearchOption.TopDirectoryOnly).Select(f => GetReportDetail(f)).ToList();
            await Task.WhenAll(topFiles);
            var topLevelReport = topFiles.Select(p => p.Result).ToList() ?? new List<ReportDetail>();

            await reportService.Write(new FileInfo(Path.Combine(di.FullName, step + "_" + appSettings.ReportDetailName)), topLevelReport.OrderBy(p => p.DateTime).ToList());

            var filesInSubfolders = di.GetDirectories().Select(d => ReportMissing(d, step)).ToList();
            var subLevelReport = (await Task.WhenAll(filesInSubfolders)).SelectMany(p => p).ToList();
            var comboReport = topLevelReport.Union(subLevelReport);

            await reportService.Write(new FileInfo(Path.Combine(di.FullName, step + "_reportMissingLocations.csv")), ComputeMissingLocations(comboReport));
            return comboReport;
        }

        private static List<ReportMissingLocation> ComputeMissingLocations(IEnumerable<ReportDetail> files)
        {
            var dates = files.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)).Select(p => p.DateTime.Date).Distinct();
            return dates.OrderBy(p => p).Select(p => new ReportMissingLocation() { Start = p, End = p.AddDays(1).AddSeconds(-1) }).ToList();
        }

        private async Task<ReportDetail> GetReportDetail(FileInfo fileInfo)
        {
            var r = new ReportDetail()
            {
                FullFileName = fileInfo.FullName,
            };
            try
            {
                ImageFile imageFile;
                ExifProperty da;
                DateTime dt = DateTime.MinValue;
                GPSLatitudeLongitude latTag;
                GPSLatitudeLongitude longTag;
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
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
                    logger.LogWarning("{File} is not a valid JPEG {Message}", fileInfo.FullName, e.Message);
                }
                catch (NotValidImageFileException e)
                {
                    logger.LogWarning("{File} is not a valid image {Message}", fileInfo.FullName, e.Message);
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

        private async Task WriteLocationFromClosestKnownIfSameDay(FileInfo fi)
        {
            logger.LogInformation(@"About to WriteLocationFromClosest for pictures listed in {Source}...", fi.FullName);
            var report = await reportService.Read<ReportDetail>(fi);
            await WriteLocationFromClosestKnownIfSameDay(report);
        }

        public async Task WriteLocation(DirectoryInfo di, LocationWriter lw)
        {
            if (lw == LocationWriter.FromClosestSameDay)
            {
                logger.LogInformation(@"About to WriteLocationFromClosest for pictures in {Source}...", di.FullName);
                await di.GetFiles(appSettings.ReportDetailName, SearchOption.AllDirectories)
                    .ParallelForEachAsync<FileInfo>(WriteLocationFromClosestKnownIfSameDay, appSettings.MaxDop);
            }
            else
            {
                var topFiles = di.GetFilesViaPattern(appSettings.PictureFilter, SearchOption.AllDirectories);
                await topFiles.ParallelForEachAsync<FileInfo>(AddlocationFromTimeLine, appSettings.MaxDop);
            }
        }
        private async Task AddlocationFromTimeLine(FileInfo fi)
        {
            try
            {
                var imageFile = await ImageFile.FromFileAsync(fi.FullName);
                var da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                _ = DateTime.TryParse(da?.ToString(), out var dt);

                if (dt == DateTime.MinValue)
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
                var firestResult = result.First();

                var latitude = firestResult.Latitude;
                var longitude = firestResult.Longitude;
                var c = new Coordinate(Convert.ToDouble(latitude), Convert.ToDouble(longitude), DateTime.Today);
                MakeLatitude(c, imageFile);
                MakeLongitude(c, imageFile);
                await imageFile.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
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
            if (fi == null || fi.Directory == null || fi.Directory.Name == appSettings.InvalidJpegFolderName)
                return;
            try
            {
                var ef = await ImageFile.FromFileAsync(fi.FullName);
                var c = new Coordinate(latitude, longitude, DateTime.Today);
                MakeLatitude(c, ef);
                MakeLongitude(c, ef);
                await ef.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
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