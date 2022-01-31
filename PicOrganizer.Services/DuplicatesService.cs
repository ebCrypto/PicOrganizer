using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PicOrganizer.Services
{
    public class DuplicatesService : IDuplicatesService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<DuplicatesService> logger;
        private readonly IFileProviderService fileProviderService;

        public DuplicatesService(AppSettings appSettings, ILogger<DuplicatesService> logger, IFileProviderService fileProviderService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.fileProviderService = fileProviderService;
        }

        public async Task MoveDuplicates(DirectoryInfo di, DirectoryInfo destination)
        {
            logger.LogDebug("About to look for duplicates in {Directory}", di.FullName);
            var fileInfos = fileProviderService.GetFilesViaPattern(di,appSettings.PictureAndVideoFilter, SearchOption.TopDirectoryOnly);
            if (fileInfos == null || !fileInfos.Any())
            {
                logger.LogDebug("No duplicates found in {Directory}", di.FullName);
            }
            else
            {
                var topFilesLength = fileInfos.Select(f => new { f.Length, f.FullName }).ToList();
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
                                if (keepingFileInfo != null)
                                    hashes[hash] = keepingFileInfo;
                            }
                            else
                                hashes.Add(hash, fileInfo);
                        }
                    }
                }
                logger.LogInformation("Looped through {TotalCount} files and found {DuplicateCount} duplicates", topFilesLength.Count, countDuplicates);
            }
            await di.GetDirectories().ToList().ParallelForEachAsync(MoveDuplicates, destination, appSettings.MaxDop);
        }
                                                          
        private FileInfo MoveDuplicate(FileInfo preExistingFile, FileInfo fileInfo, DirectoryInfo destination)
        {
            try
            {
                if (!destination.Exists)
                    destination.Create();
                var digitPre = preExistingFile.Name.Count(p => Char.IsDigit(p));
                var digitfileInfo = preExistingFile.Name.Count(p => Char.IsDigit(p));
                string destFileName;
                if (digitPre < digitfileInfo)
                {
                    destFileName = Path.Combine(destination.FullName, fileInfo.Name);
                    if (File.Exists(destFileName))
                        fileInfo.Delete();
                    else
                    {
                        if (appSettings.OutputSettings.DeleteDuplicates)
                            fileInfo.Delete();
                        else
                            fileInfo.MoveTo(destFileName);
                        logger.LogTrace("done {Action} File {File}", appSettings.OutputSettings.DeleteDuplicates ? "delete" : "move", fileInfo);
                    }
                    return preExistingFile;
                }

                destFileName = Path.Combine(destination.FullName, preExistingFile.Name);
                if (File.Exists(destFileName))
                    preExistingFile.Delete();
                else
                {
                    if (appSettings.OutputSettings.DeleteDuplicates)
                        preExistingFile.Delete();
                    else
                        preExistingFile.MoveTo(destFileName);
                    logger.LogTrace("done {Action} File {File}", appSettings.OutputSettings.DeleteDuplicates ? "delete" : "move", preExistingFile);
                }
                return fileInfo;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Can't {Action} duplicate {File1}", appSettings.OutputSettings.DeleteDuplicates? "delete": "move",  preExistingFile.FullName);
                return null;
            }
        }

        private static string ComputeMd5 (FileInfo fi)
        {
            var myFileData = File.ReadAllBytes(fi.FullName);
            return string.Join(" ", MD5.Create().ComputeHash(myFileData));
        }
    }
}