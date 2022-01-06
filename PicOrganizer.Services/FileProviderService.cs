using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using static PicOrganizer.Services.IFileProviderService;

namespace PicOrganizer.Services
{
    public class FileProviderService : IFileProviderService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<FileProviderService> logger;
        private List<string> except;

        public FileProviderService(AppSettings appSettings, ILogger<FileProviderService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            except = new List<string>();
        }

        public IEnumerable<string> GetExceptionList()
        {
            return except;
        }

        public void SetExceptionList(List<string> except)
        {
            this.except = except;
        }

        public IEnumerable<FileInfo> GetFiles(DirectoryInfo di, FileType fileType)
        {
            if (di == null)
                return Enumerable.Empty<FileInfo>();
            var fileNames = di.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories).Select(p=>p.FullName).Except(GetExceptionList());
            var fileInfos = fileNames.Select(p => new FileInfo(p));
            var result = fileType switch
            {
                FileType.Video => fileInfos
                                        .Where(p => appSettings.VideoExtensions.Contains(p.Extension.ToLower())),
                FileType.Picture => fileInfos
                                        .Where(p => appSettings.PictureExtensions.Contains(p.Extension.ToLower())),
                FileType.AllMedia => fileInfos
                                        .Where(p => appSettings.VideoExtensions.Union(appSettings.PictureExtensions).Contains(p.Extension.ToLower())),
                FileType.All => fileInfos,
                _ => Enumerable.Empty<FileInfo>(),
            };
            return result;
        }

        public IEnumerable<FileInfo> GetFilesViaPattern(DirectoryInfo source, string searchPatterns, SearchOption searchOption)
        {
            logger.LogTrace("Looking for FileInfos in {Source}, using the SearchPattern {SearchPattern} and SearchOption {SearchOption}", source.FullName, searchPatterns, searchOption);
            if (string.IsNullOrEmpty(searchPatterns))
            {
                var fileInfos = source.GetFiles("*", searchOption);
                var files = fileInfos.Select(p => p.FullName).Except(GetExceptionList());
                return fileInfos.Where(p=> files.Contains(p.FullName)); 
            }
            if (searchPatterns.Contains('|'))
            {
                string[] searchPattern = searchPatterns.Split('|');
                List<FileInfo> result = new();
                for (int i = 0; i < searchPattern.Length; i++)
                    result.AddRange(GetFilesViaPattern(source, searchPattern[i], searchOption));
                return result;
            }
            else
            {
                var fileInfos = source.GetFiles(searchPatterns, searchOption);
                var files = fileInfos.Select(p=>p.FullName).Except(GetExceptionList());
                return fileInfos.Where(p => files.Contains(p.FullName));
            }
        }
    }
}
