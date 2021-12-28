using ExifLibrary;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using PicOrganizer.Models;
using System.Text.RegularExpressions;

namespace PicOrganizer.Services
{
    public class CopyDigitalMediaService : ICopyDigitalMediaService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<CopyDigitalMediaService> _logger;
        private readonly IDirectoryNameService _directoryNameService;
        private readonly IFileNameCleanerService _fileNameCleanerService;
        private readonly IDateRecognizerService dateRecognizerService;

        public CopyDigitalMediaService(AppSettings appSettings, ILogger<CopyDigitalMediaService> logger, IDirectoryNameService directoryNameService, IFileNameCleanerService fileNameCleanerService, IDateRecognizerService dateRecognizerService)
        {
            this.appSettings = appSettings;
            _logger = logger;
            _directoryNameService = directoryNameService;
            _fileNameCleanerService = fileNameCleanerService;
            this.dateRecognizerService = dateRecognizerService;
        }

        public async Task Copy(DirectoryInfo from, DirectoryInfo to)
        {
            _logger.LogInformation(@"About to Copy Videos from {Source}...", from.FullName);
            await from.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories)
                .Where(p => appSettings.VideoExtensions.Contains(p.Extension.ToLower()))
                .ParallelForEachAsync<FileInfo, DirectoryInfo>(CopyOneVideo, to, appSettings.MaxDop);
            _logger.LogInformation(@"About to Copy Pictures from {Source}...", from.FullName);
            await from.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories)
                .Where(p => appSettings.PictureExtensions.Contains(p.Extension.ToLower()))
                .ParallelForEachAsync<FileInfo, DirectoryInfo>(CopyOnePicture, to, appSettings.MaxDop);
        }

        private async Task CopyOneVideo(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                _logger.LogTrace("Processing {File}", fileInfo.FullName);
                
                var destination = appSettings.VideosFolderName;
                DateTime dateInferred = dateRecognizerService.InferDateFromName(fileInfo.Name);
                if (dateInferred == DateTime.MinValue)
                    dateInferred = dateRecognizerService.InferDateFromName(_fileNameCleanerService.CleanName(fileInfo.Name));
                if (dateInferred == DateTime.MinValue)
                    dateInferred = dateRecognizerService.InferDateFromName(_fileNameCleanerService.CleanName(fileInfo.Directory?.Name));
                if (dateInferred != DateTime.MinValue)
                    destination = Path.Combine(destination,_directoryNameService.MakeName(dateInferred));

                var targetDirectory = new DirectoryInfo(Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    _logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                }
                string cleanName = _fileNameCleanerService.AddParentDirectoryToFileName(fileInfo);
                fileInfo.CopyTo(Path.Combine(targetDirectory.FullName, cleanName), true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task CopyOnePicture(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                _logger.LogTrace("Processing {File}", fileInfo.FullName);
                ImageFile imageFile;
                string destination = appSettings.UnkownFolderName;
                DateTime dateTimeOriginal = DateTime.MinValue;
                DateTime dateInferred = DateTime.MinValue;
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    ExifProperty? tag;
                    tag = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(tag?.ToString(), out dateTimeOriginal);
                    string cleanFolderName = _fileNameCleanerService.CleanName(fileInfo.Directory?.Name);
                    if (dateTimeOriginal == DateTime.MinValue)
                        dateInferred = dateRecognizerService.InferDateFromName(fileInfo.Name);
                    if (dateTimeOriginal == DateTime.MinValue && dateInferred == DateTime.MinValue)
                        dateInferred = dateRecognizerService.InferDateFromName(_fileNameCleanerService.CleanName(fileInfo.Name));
                    if (dateTimeOriginal == DateTime.MinValue && dateInferred == DateTime.MinValue)
                    {
                        dateInferred = dateRecognizerService.InferDateFromName(cleanFolderName);
                    }

                    if (dateInferred == DateTime.MinValue && !cleanFolderName.ToLowerInvariant().Contains(appSettings.Scanned))
                        destination = _directoryNameService.MakeName(dateTimeOriginal);
                    else
                        destination = _directoryNameService.MakeName(dateInferred);
                }
                catch (NotValidJPEGFileException)
                {
                    destination = appSettings.InvalidJpegFolderName;
                }
                catch (NotValidImageFileException)
                {
                    _logger.LogDebug("NotValidImageFileException encoutered, assuming {File} is a Video", fileInfo.Name);
                    destination = appSettings.VideosFolderName;
                }

                bool sourceWhatsapp = SourceWhatsApp(fileInfo);
                var targetDirectory = SourceWhatsApp(fileInfo)? 
                                            new DirectoryInfo(Path.Combine(to.FullName, sourceWhatsapp ? appSettings.WhatsappFolderName : string.Empty, destination)):
                                            new DirectoryInfo(Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    _logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                } // TODO move this to copy?

                await Copy(fileInfo, targetDirectory, dateInferred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task Copy(FileInfo fileInfo, DirectoryInfo targetDirectory, DateTime dateInferred)
        {
            string cleanName = _fileNameCleanerService.AddParentDirectoryToFileName(fileInfo);
            string destFileName = Path.Combine(targetDirectory.FullName, cleanName);
            fileInfo.CopyTo(destFileName, true);
            if (dateInferred != DateTime.MinValue)
            {
                try
                {
                    var imageFile = await ImageFile.FromFileAsync(destFileName);
                    imageFile.Properties.Set(ExifTag.DateTimeOriginal, dateInferred);
                    await imageFile.SaveAsync(destFileName);
                    _logger.LogDebug("Added date {Date} to file {File}", dateInferred.ToString(), destFileName);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Unable to add date {Date} to file {File}", dateInferred.ToString(), destFileName);
                }
            }
        }

        private bool SourceWhatsApp(FileInfo fi)
        {
            var regex = new Regex(appSettings.WhatsappNameRegex);
            return regex.IsMatch(fi.Name);
        }
    }
}