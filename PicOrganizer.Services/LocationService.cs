using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Linq;

namespace PicOrganizer.Services
{
    public class LocationService : ILocationService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<LocationService> logger;
        private readonly IReportWriterService reportWriterService;

        public LocationService(AppSettings appSettings, ILogger<LocationService> logger, IReportWriterService reportWriterService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.reportWriterService = reportWriterService;
        }

        public async Task<IEnumerable<ReportDetail>> Report(DirectoryInfo di)
        {
            logger.LogDebug("About to create Report in {Directory}", di.FullName);
            var topFiles = di.GetFilesViaPattern(appSettings.PictureExtensions, SearchOption.TopDirectoryOnly).Select(f => LogInfo(f)).ToList();
            await Task.WhenAll(topFiles);
            var topLevelReport = topFiles.Select(p => p.Result).ToList() ?? new List<ReportDetail>();
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportDetail.csv")), topLevelReport.OrderBy(p=>p.DateTime).ToList());
            var folders = di.GetDirectories().Select(d => Report(d)).ToList() ;
            var subLevelReport = (await Task.WhenAll(folders)).SelectMany(p=>p).ToList();
            var comboReport = topLevelReport.Union(subLevelReport);
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportMissingLocations.csv")), ComputeMissingLocations(comboReport));
            return comboReport;
        }

        private List<ReportMissingLocation> ComputeMissingLocations(IEnumerable<ReportDetail> files)
        {
            var dates = files.Where(p => string.IsNullOrEmpty(p.Latitude) || string.IsNullOrEmpty(p.Longitude)).Select(p => p.DateTime.Date).Distinct();
            return dates.OrderBy(p=>p).Select(p => new ReportMissingLocation() { Start = p, End = p.AddDays(1).AddSeconds(-1) }).ToList();
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
                catch (NotValidJPEGFileException e)
                {
                    logger.LogWarning("{File} not a valid JPEG {Message}", fileInfo.FullName, e.Message);
                }
                catch (NotValidImageFileException e)
                {
                    logger.LogWarning("{File} not a valid image {Message}", fileInfo.FullName, e.Message);
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