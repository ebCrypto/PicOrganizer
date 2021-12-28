using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Security.Cryptography;
using System.Linq;
using System.Collections.Concurrent;

namespace PicOrganizer.Services
{
    public class DuplicatesService : IDuplicatesService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<DuplicatesService> logger;
        private readonly IReportReadWriteService reportWriterService;

        public DuplicatesService(AppSettings appSettings, ILogger<DuplicatesService> logger, IReportReadWriteService reportWriterService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.reportWriterService = reportWriterService;
        }

        public async Task MoveDuplicates(DirectoryInfo di, DirectoryInfo destination)
        {
            logger.LogDebug("About to look for duplicates in {Directory}", di.FullName);
            var topFilesLength = di.GetFilesViaPattern(appSettings.AllFileExtensions, SearchOption.TopDirectoryOnly).Select(f => new { f.Length, f.FullName }).ToList();
            int countDuplicates = 0;
            if (topFilesLength.Any())
            {
                var lengthCounts = topFilesLength.GroupBy(p => p.Length).Select(g => new { g.Key, Count = g.Count() }).ToDictionary(g => g.Key, g => g.Count);
                foreach (var lengthCount in lengthCounts.Where(p => p.Value > 1))
                {
                    var files = topFilesLength.Where(p => p.Length == lengthCount.Key).ToList();
                    logger.LogTrace("Found Potential Duplicates {Names}", string.Join(", ", files.Select(p => p.FullName)));
                    var hashes = new Dictionary<string, FileInfo>();
                    foreach (var f in files)
                    {
                        var fileInfo = new FileInfo(f.FullName);
                        var hash = ComputeMd5(fileInfo);
                        if (hashes.ContainsKey(hash))
                        {
                            FileInfo preExistingFile = hashes[hash];
                            if (preExistingFile.FullName == fileInfo.FullName)
                                continue;
                            logger.LogInformation("Duplicates Found. {File1} & {File2} have the same hash", preExistingFile, fileInfo);
                            countDuplicates++;
                            FileInfo keepingFileInfo = MoveDuplicate(preExistingFile, fileInfo, destination);
                            hashes[hash] = keepingFileInfo;
                        }
                        else
                            hashes.Add(hash, fileInfo);
                    }
                }
            }
            logger.LogInformation("Looped through {TotalCount} and found {DuplicateCount} duplicates", topFilesLength.Count, countDuplicates);
            await di.GetDirectories().ToList().ParallelForEachAsync<DirectoryInfo, DirectoryInfo>(MoveDuplicates, destination, appSettings.MaxDop);
        }
                                                          
        private FileInfo MoveDuplicate(FileInfo preExistingFile, FileInfo fileInfo, DirectoryInfo destination)
        {
            if (!destination.Exists)
                destination.Create();
            var digitPre = preExistingFile.Name.Count(p => Char.IsDigit(p));
            var digitfileInfo = preExistingFile.Name.Count(p => Char.IsDigit(p));

            if (digitPre < digitfileInfo)
            {
                fileInfo.MoveTo(Path.Combine(destination.FullName , fileInfo.Name));
                return preExistingFile;
            }
            preExistingFile.MoveTo(Path.Combine(destination.FullName, preExistingFile.Name));
            return fileInfo;
        }

        private static string ComputeMd5 (FileInfo fi)
        {
            var myFileData = File.ReadAllBytes(fi.FullName);
            return string.Join(" ", MD5.Create().ComputeHash(myFileData));
        }
    }
}