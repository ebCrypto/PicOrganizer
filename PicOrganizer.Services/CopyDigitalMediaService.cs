using ExifLibrary;
using System.Linq;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Text.RegularExpressions;

namespace PicOrganizer.Services
{
    public class CopyDigitalMediaService : ICopyDigitalMediaService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<CopyDigitalMediaService> logger;
        private readonly IFileNameService fileNameService;
        private readonly IDateRecognizerService dateRecognizerService;
        private readonly IFileProviderService fileProviderService;

        public CopyDigitalMediaService(AppSettings appSettings, ILogger<CopyDigitalMediaService> logger, IFileNameService fileNameService, IDateRecognizerService dateRecognizerService, IFileProviderService fileProviderService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.fileNameService = fileNameService;
            this.dateRecognizerService = dateRecognizerService;
            this.fileProviderService = fileProviderService;
        }

        public async Task Copy(DirectoryInfo from, DirectoryInfo to)
        {
            logger.LogInformation(@"About to Copy Videos from {Source}...", from.FullName);
            var videos = fileProviderService.GetFiles(from, IFileProviderService.FileType.Video);
            logger.LogDebug("Found {Count} Video(s) in {From}", videos.Count(), from);
            await videos.ParallelForEachAsync(CopyOneVideo, to, appSettings.MaxDop);

            logger.LogInformation(@"About to Copy Pictures from {Source}...", from.FullName);
            var pictures = fileProviderService.GetFiles(from, IFileProviderService.FileType.Picture);
            await pictures.ParallelForEachAsync(CopyOnePicture, to, appSettings.MaxDop);
            logger.LogDebug("Found {Count} Pictures(s) in {From}", pictures.Count(), from);
        }

        private async Task CopyOneVideo(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                logger.LogTrace("Processing {File}", fileInfo.FullName);
                
                var destination = appSettings.OutputSettings.VideosFolderName;
                DateTime dateInferred = dateRecognizerService.InferDateFromName(fileInfo.Name);
                if (!dateRecognizerService.Valid(dateInferred))
                    dateInferred = dateRecognizerService.InferDateFromName(fileNameService.CleanName(fileInfo.Name));
                if(!dateRecognizerService.Valid(dateInferred))
                    dateInferred = dateRecognizerService.InferDateFromName(fileNameService.CleanName(fileInfo.Directory?.Name));
                if (!dateRecognizerService.Valid(dateInferred))
                    destination = Path.Combine(destination, fileNameService.MakeDirectoryName(dateInferred));

                var targetDirectory = new DirectoryInfo(Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                }
                string cleanName = fileNameService.AddParentDirectoryToFileName(fileInfo);
                fileInfo.CopyTo(Path.Combine(targetDirectory.FullName, cleanName), true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task CopyOnePicture(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                if ( appSettings.InputSettings.ExcludedFiles.Contains(fileInfo.Name))
                {
                    logger.LogInformation("Skipping file {File} because it is part of the exclusion list", fileInfo.FullName);
                    return;
                }
                logger.LogTrace("Processing {File}", fileInfo.FullName);
                ImageFile imageFile;
                string destination = appSettings.OutputSettings.UnkownFolderName;
                DateTime dateTimeOriginal = DateTime.MinValue;
                DateTime dateInferred = DateTime.MinValue;
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    ExifProperty? tag;
                    tag = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(tag?.ToString(), out dateTimeOriginal);
                    string cleanFolderName = fileNameService.CleanName(fileInfo.Directory?.Name);
                    if (!dateRecognizerService.Valid(dateTimeOriginal) || cleanFolderName.ToLowerInvariant().Contains(appSettings.InputSettings.Scanned))
                        dateInferred = dateRecognizerService.InferDateFromName(fileInfo.Name);
                    if ((!dateRecognizerService.Valid(dateTimeOriginal) || cleanFolderName.ToLowerInvariant().Contains(appSettings.InputSettings.Scanned)) && !dateRecognizerService.Valid(dateInferred))
                        dateInferred = dateRecognizerService.InferDateFromName(fileNameService.CleanName(fileInfo.Name));
                    if ((!dateRecognizerService.Valid(dateTimeOriginal) || cleanFolderName.ToLowerInvariant().Contains(appSettings.InputSettings.Scanned)) && !dateRecognizerService.Valid(dateInferred))
                        dateInferred = dateRecognizerService.InferDateFromName(cleanFolderName);
                    
                    destination = fileNameService.MakeDirectoryName(!dateRecognizerService.Valid(dateInferred)? dateTimeOriginal: dateInferred);
                }
                catch (NotValidJPEGFileException)
                {
                    destination = appSettings.OutputSettings.InvalidJpegFolderName;
                }
                catch (NotValidImageFileException)
                {
                    logger.LogWarning("NotValidImageFileException encoutered, assuming {File} is a Video", fileInfo.Name);
                    destination = appSettings.OutputSettings.VideosFolderName;
                }

                bool sourceWhatsapp = SourceWhatsApp(fileInfo);
                var targetDirectory = SourceWhatsApp(fileInfo)? 
                                            new DirectoryInfo(Path.Combine(to.FullName, sourceWhatsapp ? appSettings.OutputSettings.WhatsappFolderName : string.Empty, destination)):
                                            new DirectoryInfo(Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                } // TODO move this to copy?

                await Copy(fileInfo, targetDirectory, dateInferred);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task Copy(FileInfo fileInfo, DirectoryInfo targetDirectory, DateTime dateInferred)
        {
            string cleanName = fileNameService.AddParentDirectoryToFileName(fileInfo);
            string destFileName = Path.Combine(targetDirectory.FullName, cleanName);
            fileInfo.CopyTo(destFileName, true);
            if (dateRecognizerService.Valid(dateInferred))
            {
                try
                {
                    var imageFile = await ImageFile.FromFileAsync(destFileName);
                    imageFile.Properties.Set(ExifTag.DateTimeOriginal, new ExifDateTime(ExifTag.DateTimeOriginal, dateInferred));
                    await imageFile.SaveAsync(destFileName);
                    logger.LogDebug("Added date {Date} to file {File}", dateInferred.ToString(), destFileName);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Unable to add date {Date} to file {File}", dateInferred.ToString(), destFileName);
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