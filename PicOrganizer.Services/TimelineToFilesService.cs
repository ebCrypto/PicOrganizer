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
        private List<ReportMissingLocation> timeline;

        public List<ReportMissingLocation> GetTimeline()
        {
            return timeline;
        }

        private void SetTimeline(List<ReportMissingLocation> value)
        {
            timeline = value;
        }

        public TimelineToFilesService(AppSettings appSettings, ILogger<TimelineToFilesService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
        }

        public void LoadTimeLine(FileInfo csv)
        {
            using var reader = new StreamReader(csv.FullName);
            using CsvReader? csvReader = new(reader, CultureInfo.InvariantCulture);
            SetTimeline(csvReader.GetRecords<ReportMissingLocation>().ToList());
            VerifyTimeLine();
        }

        public void VerifyTimeLine()
        {
            DateTime? lastEnd = null;   
            foreach ( var time in GetTimeline())
            {
                if (lastEnd != null && time.Start != null && time.Start < lastEnd)
                    logger.LogWarning("Unexpected timeline element starting at {Start}, which is before {LastEnd}", time.Start.ToString(), lastEnd.ToString());
                lastEnd = time.End;
            }
        }
    }
}
