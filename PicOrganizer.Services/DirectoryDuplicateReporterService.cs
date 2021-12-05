using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Security.Cryptography;
using System.Linq;

namespace PicOrganizer.Services
{
    public class DirectoryDuplicateReporterService : IDirectoryDuplicateReporterService
    {
        private readonly ILogger<DirectoryDuplicateReporterService> logger;
        private readonly IReportWriterService reportWriterService;

        public DirectoryDuplicateReporterService(ILogger<DirectoryDuplicateReporterService> logger, IReportWriterService reportWriterService)
        {
            this.logger = logger;
            this.reportWriterService = reportWriterService;
        }

        public async Task Report(DirectoryInfo di)
        {
            logger.LogDebug("About to create Report in {Directory}", di.FullName);
            var topFiles = di.GetFiles("*.jpg", SearchOption.TopDirectoryOnly).Select(f => LogInfo(f)).ToList();
            await Task.WhenAll(topFiles);
            List<ReportDetail>? topLevelReport = topFiles.Select(p => p.Result).ToList();
            if (topLevelReport == null)
                return;
            var dateCounts = topLevelReport.GroupBy(p => p.DateTime).Select(g => new { g.Key, Count = g.Count() }).ToDictionary(g => g.Key, g => g.Count);

            int countDuplicates = 0;
            foreach (var dateCount in dateCounts.Where(p => p.Value > 1))
            {
                var files = topLevelReport.Where(p => p.DateTime == dateCount.Key).ToList();
                logger.LogDebug("Found Potential Duplicates {Names}", String.Join(", ", files.Select(p => p.FullFileName)));
                var hashes = new Dictionary<string, FileInfo>();
                foreach (ReportDetail? f in files)
                {
                    var fileInfo = new FileInfo(f.FullFileName);
                    var hash = ComputeMd5(fileInfo);
                    if (hashes.ContainsKey(hash))
                    {
                        FileInfo preExistingFile = hashes[hash];
                        if (preExistingFile.FullName == fileInfo.FullName)
                            continue;
                        logger.LogWarning("{File1} & {File2} have the same hash", preExistingFile, fileInfo);
                        countDuplicates++;
                        f.Duplicates += " " + hashes[hash].Name;
                    }
                    else
                        hashes.Add(hash, fileInfo);
                }
            }
            logger.LogInformation("Looped through {TotalCount} and found {DuplicateCount} duplicates", topLevelReport.Count, countDuplicates);
            await reportWriterService.Write(new FileInfo(Path.Combine(di.FullName, "reportDuplicates.csv")), topLevelReport.OrderBy(p => p.DateTime).ToList());

            var folders = di.GetDirectories().Select(d => Report(d)).ToList();
            await Task.WhenAll(folders);
        }

        private static string ComputeMd5 (FileInfo fi)
        {
            var myFileData = File.ReadAllBytes(fi.FullName);
            return String.Join(" ", MD5.Create().ComputeHash(myFileData));
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
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    da = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(da?.ToString(), out dt);
                    r.DateTime = dt;
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