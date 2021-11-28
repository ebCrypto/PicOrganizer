using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;

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

        public async Task Report(DirectoryInfo di)
        {
            logger.LogDebug("About to create Report in {Directory}", di.FullName);
            var topFiles = di.GetFiles("*.*", SearchOption.TopDirectoryOnly).Select(f => LogInfo(f)).ToList();
            await Task.WhenAll(topFiles);
            var records = topFiles.Select(p => p.Result).ToList();
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportDetail.csv")), records);

            var folders = di.GetDirectories().Select(d => Report(d)).ToList();
            await Task.WhenAll(folders);
        }

        private async Task<ReportDetail> LogInfo(FileInfo fileInfo)
        {
            var r = new ReportDetail()
            {
                FileName = fileInfo.Name,
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