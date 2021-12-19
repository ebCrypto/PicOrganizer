using System.Globalization;
using CoordinateSharp;
using CsvHelper;
using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public class TimelineToFilesService : ITimelineToFilesService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<TimelineToFilesService> logger;

        public List<ReportMissingLocation> Timeline { get; private set; }

        public TimelineToFilesService(AppSettings appSettings, ILogger<TimelineToFilesService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
        }

        public async Task AddFloatCoordinatesToImage(string latitude, string longitude, FileInfo fi)
        {
            try
            {
                var ef = await ImageFile.FromFileAsync(fi.FullName);
                var c = new Coordinate(Convert.ToDouble(latitude), Convert.ToDouble(longitude), DateTime.Today);
                MakeLatitude(c, ef);
                MakeLongitude(c, ef);
                await ef.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add coordinates to {File}", fi.FullName);
            }
        }

        private static void MakeLongitude(Coordinate coordinate, ImageFile ef)
        {
            string lon = coordinate.Longitude.ToString();
            if (string.IsNullOrEmpty(lon))
                return;
            var items = lon.Split(" ");

            ef.Properties.Set(ExifTag.GPSLongitude, GetFloat(items[1]), GetFloat(items[2]), GetFloat(items[3]));
            ef.Properties.Set(ExifTag.GPSLongitudeRef, items[0] == "E" ? GPSLongitudeRef.East : GPSLongitudeRef.West);
        }

        private static void MakeLatitude(Coordinate coordinate, ImageFile ef)
        {
            // N 55º 40' 35.883"
            string lat = coordinate.Latitude.ToString();
            if (string.IsNullOrEmpty(lat))
                return;
            var items = lat.Split(" ");

            ef.Properties.Set(ExifTag.GPSLatitude, GetFloat(items[1]), GetFloat(items[2]), GetFloat(items[3]));
            ef.Properties.Set(ExifTag.GPSLatitudeRef, items[0] == "N" ? GPSLatitudeRef.North : GPSLatitudeRef.South);
        }

        private static float GetFloat(string s)
        {
            return Convert.ToSingle(s.Substring(0, s.Length - 1));
        }

        public void LoadTimeLine(FileInfo csv)
        {
            using var reader = new StreamReader(csv.FullName);
            using CsvReader? csvReader = new(reader, CultureInfo.InvariantCulture);
            Timeline = csvReader.GetRecords<ReportMissingLocation>().ToList();
            VerifyTimeLine();
        }

        public void VerifyTimeLine()
        {

        }

        public async Task AddlocationFromTimeLine(FileInfo fi)
        {
            var imageFile = await ImageFile.FromFileAsync(fi.FullName);
            var da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
            _ = DateTime.TryParse(da?.ToString(), out var dt);

            ReportMissingLocation? result = Timeline.Where(p => p.Start <= dt && p.End >= dt).SingleOrDefault();
            if ( result == null)
            {
                logger.LogError("Unexpected TimeLine");
                return;
            }
            try
            {
                var latitude = result.Latitude;
                var longitude = result.Longitude;
                var c = new Coordinate(Convert.ToDouble(latitude), Convert.ToDouble(longitude), DateTime.Today);
                MakeLatitude(c, imageFile);
                MakeLongitude(c, imageFile);
                await imageFile.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add coordinates to {File}", fi.FullName);
            }
        }

        public async Task AddlocationFromTimeLine(DirectoryInfo di)
        {
            var topFiles = di.GetFilesViaPattern(appSettings.PictureExtensions, SearchOption.TopDirectoryOnly);
            await topFiles.ParallelForEachAsync<FileInfo>(AddlocationFromTimeLine);
        }
    }
}
