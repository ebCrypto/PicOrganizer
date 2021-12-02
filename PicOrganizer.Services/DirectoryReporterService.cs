using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Linq;

namespace PicOrganizer.Services
{
    public class DirectoryReporterService : IDirectoryReporterService
    {
        private readonly ILogger<DirectoryReporterService> logger;
        private readonly IReportWriterService reportWriterService;

        public DirectoryReporterService(ILogger<DirectoryReporterService> logger, IReportWriterService reportWriterService)
        {
            this.logger = logger;
            this.reportWriterService = reportWriterService;
        }

        public async Task<IEnumerable<ReportDetail>> LocationReport(DirectoryInfo di)
        {
            logger.LogDebug("About to create Report in {Directory}", di.FullName);
            var topFiles = di.GetFiles("*.jpg", SearchOption.TopDirectoryOnly).Select(f => LogInfo(f)).ToList();
            await Task.WhenAll(topFiles);
            var topLevelReport = topFiles.Select(p => p.Result).ToList() ?? new List<ReportDetail>();
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportDetail.csv")), topLevelReport.OrderBy(p=>p.DateTime).ToList());
            var folders = di.GetDirectories().Select(d => LocationReport(d)).ToList() ;
            var subLevelReport = (await Task.WhenAll(folders)).SelectMany(p=>p).ToList();
            var comboReport = topLevelReport.Union(subLevelReport);
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportMissingLocations.csv")), ComputeMissingLocations(comboReport));
            return comboReport;
        }

        private List<ReportMissingLocation> ComputeMissingLocations(IEnumerable<ReportDetail> files)
        {
            var timeLine = files.Select(p => new TimeLineItem() {
                DateTime = p.DateTime, 
                LocationMissing = string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude),
                SampleFileName = p.FullFileName
            }
            ).OrderBy(p => p.DateTime).ToList();
            if (!timeLine.Any())
                return null;
            for (int i = 1; i < timeLine.Count; i++)
            {
                if (timeLine[i - 1].LocationMissing)
                    timeLine[i].CanBeSkipped = true;
            }

            return timeLine.Where(p => p.LocationMissing && !p.CanBeSkipped).Select(p => new ReportMissingLocation()
            {
                SampleFileName = p.SampleFileName,
                Start = p.DateTime,
                End = (timeLine.FirstOrDefault(q => !q.LocationMissing && q.DateTime > p.DateTime)?? timeLine.OrderByDescending(r=>r.DateTime).FirstOrDefault())?.DateTime
            }).ToList();
        }

        private class TimeLineItem
        {
            public DateTime DateTime { get; set; }
            public bool LocationMissing { get; set; }
            public bool CanBeSkipped { get; set; }
            public string SampleFileName { get; set; }
        }

        private async Task<ReportDetail> LogInfo(FileInfo fileInfo)
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
                GPSLatitudeLongitude latTag = null;
                GPSLatitudeLongitude longTag = null;
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(da?.ToString(), out dt);
                    latTag = imageFile.Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude);
                    longTag = imageFile.Properties.Get<GPSLatitudeLongitude>(ExifTag.GPSLongitude);
                    r.DateTime = dt;
                    r.Latitude = latTag?.ToString();
                    r.Longitude = longTag?.ToString();
                }
                catch (NotValidImageFileException)
                {
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
            return r;
        }
    }
}