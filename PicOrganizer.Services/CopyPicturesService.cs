using ExifLibrary;
using Microsoft.Extensions.Logging;

namespace PicOrganizer.Services
{
    public class CopyPicturesService : ICopyPicturesService
    {
        private readonly ILogger<CopyPicturesService> _logger;
        private readonly IDirectoryNameService _directoryNameService;
        private readonly IFileNameCleanerService _fileNameCleanerService;
        private readonly string[] extensions;

        public CopyPicturesService(ILogger<CopyPicturesService> logger, IDirectoryNameService directoryNameService, IFileNameCleanerService fileNameCleanerService)
        {
            _logger = logger;
            _directoryNameService = directoryNameService;
            _fileNameCleanerService = fileNameCleanerService;
            extensions = new [] { ".jpeg", ".jpg", ".avi", ".mpg", ".mpeg", ".mp4", ".mov", ".wmv", ".mkv", ".png" };
        }

        public async Task Copy(DirectoryInfo from, DirectoryInfo to)
        {
            _logger.LogInformation(@"Processing {Source}", from.FullName); 
            //var tasks = from.GetFiles("*.*", SearchOption.AllDirectories).Where(p=> extensions.Contains(p.Extension.ToLower() )).Select(f => CopyOne(f, to)).ToList();
            //await Task.WhenAll(tasks);

            await from.GetFiles("*.*", SearchOption.AllDirectories).Where(p => extensions.Contains(p.Extension.ToLower())).ParallelForEachAsync<FileInfo, DirectoryInfo>(CopyOne, to);
        }

        private async Task CopyOne(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                _logger.LogTrace("Processing {File}", fileInfo.FullName);
                ImageFile imageFile;
                DateTime dateTimeOriginal = DateTime.MinValue;
                string? destination;
                try
                {
                    imageFile = ImageFile.FromFile(fileInfo.FullName);
                    ExifProperty? tag;
                    tag = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(tag?.ToString(), out dateTimeOriginal);
                    destination = _directoryNameService.GetName(dateTimeOriginal);
                }
                catch (NotValidImageFileException)
                {
                    destination = "Videos";
                }

                var targetDirectory = new DirectoryInfo( Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    _logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                }
                await Task.Run(() =>
                {
                    fileInfo.CopyTo(Path.Combine(targetDirectory.FullName, _fileNameCleanerService.MakeDirectoryName(fileInfo)), true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }
    }
}
