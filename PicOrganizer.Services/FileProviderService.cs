using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using static PicOrganizer.Services.IFileProviderService;

namespace PicOrganizer.Services
{
    public class FileProviderService : IFileProviderService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<FileProviderService> logger;
        private List<string> processedPreviously;

        public FileProviderService(AppSettings appSettings, ILogger<FileProviderService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            processedPreviously = new List<string>();
        }

        public void SetProcessedPreviously(List<string> processedPreviously)
        {
            this.processedPreviously = processedPreviously;
        }

        public IEnumerable<FileInfo> GetFiles(DirectoryInfo di, FileType fileType, bool includeAlreadyProcessed)
        {
            if (di == null)
                return Enumerable.Empty<FileInfo>();

            var pattern = fileType switch
            {
                FileType.Video => appSettings.VideoFilter,
                FileType.Picture => appSettings.PictureFilter,
                FileType.AllMedia => appSettings.PictureAndVideoFilter,
                _ => appSettings.AllFileExtensions
            };

            var fileNamesNotProcessed = GetFilesViaPattern(di, pattern, SearchOption.AllDirectories, includeAlreadyProcessed).Select(p => p.FullName);
            logger.LogInformation("{DirectoryName}: Found {AllCount} {FileType}(s).",
                di.FullName, fileNamesNotProcessed.Count(), fileType) ; 
            var fileInfos = fileNamesNotProcessed.Select(p => new FileInfo(p));
            return fileInfos;
        }

        public IEnumerable<FileInfo> GetFilesViaPattern(DirectoryInfo di, string searchPatterns, SearchOption searchOption, bool includeAlreadyProcessed)
        {
            //TODO should store GetFilesViaPattern in a cache
            logger.LogTrace("Looking for FileInfos in {Source}, using the SearchPattern {SearchPattern} and SearchOption {SearchOption}", di.FullName, searchPatterns, searchOption);
            if (string.IsNullOrEmpty(searchPatterns))
            {
                var fileInfos = di.GetFiles(appSettings.AllFileExtensions, searchOption);
                var allFiles = fileInfos.Select(p => p.FullName);
                var files = includeAlreadyProcessed ? allFiles: allFiles.Except(processedPreviously);
                logger.Log(files.Any() && !includeAlreadyProcessed ? LogLevel.Information : LogLevel.Debug, "{Directory}: {allCount} Files.{AlreadyProcessed}", di.FullName, allFiles.Count(), includeAlreadyProcessed ? string.Format($" {files.Count()} Files not already processed.") : string.Empty);
                return fileInfos.Where(p=> files.Contains(p.FullName)); 
            }
            if (searchPatterns.Contains('|'))
            {
                var searchPattern = searchPatterns.Split('|');
                List<FileInfo> result = new();
                for (int i = 0; i < searchPattern.Length; i++)
                    result.AddRange(GetFilesViaPattern(di, searchPattern[i], searchOption, includeAlreadyProcessed));
                return result;
            }
            else
            {
                var fileInfos = di.GetFiles(searchPatterns, searchOption);
                var allFiles = fileInfos.Select(p => p.FullName);
                var files = includeAlreadyProcessed ? allFiles : allFiles.Except(processedPreviously);
                logger.Log(files.Any() && !includeAlreadyProcessed ? LogLevel.Information : LogLevel.Debug, "{Directory}: {allCount} Files.{AlreadyProcessed}", di.FullName, allFiles.Count(), includeAlreadyProcessed ? string.Format($" {files.Count()} Files not already processed.") : string.Empty);
                return fileInfos.Where(p => files.Contains(p.FullName));
            }
        }
    }
}
