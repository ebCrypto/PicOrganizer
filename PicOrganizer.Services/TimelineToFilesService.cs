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
        private readonly ILocationService locationService;

        public List<ReportMissingLocation> Timeline { get; private set; }

        public TimelineToFilesService(AppSettings appSettings, ILogger<TimelineToFilesService> logger, ILocationService locationService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.locationService = locationService;
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
            DateTime? lastEnd = null;   
            foreach ( var time in Timeline)
            {
                if (lastEnd != null && time.Start != null && time.Start < lastEnd)
                    logger.LogWarning("Unexpected timeline element starting at {Start}, which is before {LastEnd}", time.Start.ToString(), lastEnd.ToString());
                lastEnd = time.End;
            }
        }

        public async Task AddlocationFromTimeLine(FileInfo fi)
        {
            try
            {
                var imageFile = await ImageFile.FromFileAsync(fi.FullName);
                var da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                _ = DateTime.TryParse(da?.ToString(), out var dt);

                if (dt == DateTime.MinValue)
                    return;

                var result = Timeline.Where(p => p.Start <= dt && p.End >= dt && !string.IsNullOrEmpty(p.Latitude) && !string.IsNullOrEmpty(p.Longitude));
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
                locationService.MakeLatitude(c, imageFile);
                locationService.MakeLongitude(c, imageFile);
                await imageFile.SaveAsync(fi.FullName);
                logger.LogDebug("Added {Latitude} and Longitude {Longitude} to {File}", latitude, longitude, fi.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "unable to add coordinates to {File}", fi.FullName);
            }
        }

        public async Task AddlocationFromTimeLine(DirectoryInfo di)
        {
            var topFiles = di.GetFilesViaPattern(appSettings.PictureFilter, SearchOption.AllDirectories);
            await topFiles.ParallelForEachAsync<FileInfo>(AddlocationFromTimeLine, appSettings.MaxDop);
        }
    }
}
