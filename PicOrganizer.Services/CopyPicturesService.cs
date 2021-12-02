using ExifLibrary;
using Microsoft.Extensions.Logging;

namespace PicOrganizer.Services
{
    public class CopyPicturesService : ICopyPicturesService
    {
        private readonly ILogger<CopyPicturesService> _logger;
        private readonly IDirectoryNameService _directoryNameService;

        public CopyPicturesService(ILogger<CopyPicturesService> logger, IDirectoryNameService directoryNameService)
        {
            _logger = logger;
            _directoryNameService = directoryNameService;
        }

        public async Task Copy(DirectoryInfo from, DirectoryInfo to)
        {
            _logger.LogInformation(@"Processing {Source}", from.FullName); 
            var tasks = from.GetFiles("*.*", SearchOption.AllDirectories).Where(p=>p.Extension.ToLower() != ".json").Select(f => CopyOne(f, to)).ToList();
            await Task.WhenAll(tasks);
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
                    fileInfo.CopyTo(Path.Combine(to.FullName, destination, fileInfo.Name), true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }
    }
}
