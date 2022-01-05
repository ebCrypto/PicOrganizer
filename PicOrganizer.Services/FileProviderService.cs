using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using static PicOrganizer.Services.IFileProviderService;

namespace PicOrganizer.Services
{
    public class FileProviderService : IFileProviderService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<FileProviderService> logger;

        public FileProviderService(AppSettings appSettings, ILogger<FileProviderService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
        }

        private IEnumerable<FileInfo> except;

        public void SetExcept (IEnumerable<FileInfo> except)
        {
            logger.LogInformation("Adding {Count} files to the exception list", except?.Count());
            this.except = except;
        }

        public IEnumerable<FileInfo> GetFiles(DirectoryInfo di, FileType fileType)
        {
            if (di == null)
                return Enumerable.Empty<FileInfo>();
            var fileInfos = di.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories).Except(except);
            return fileType switch
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
        }

        public IEnumerable<FileInfo> GetFilesViaPattern(DirectoryInfo source, string searchPatterns, SearchOption searchOption)
        {
            if (string.IsNullOrEmpty(searchPatterns))
                return source.GetFiles("*",searchOption).Except(except);
            if (searchPatterns.Contains('|'))
            {
                string[] searchPattern = searchPatterns.Split('|');
                List<FileInfo> result = new();
                for (int i = 0; i < searchPattern.Length; i++)
                    result.AddRange(GetFilesViaPattern(source, searchPattern[i], searchOption));
                return result;
            }
            else
                return source.GetFiles(searchPatterns, searchOption).Except(except);
        }
    }
}
