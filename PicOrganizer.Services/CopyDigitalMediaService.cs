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
        private readonly IRunDataService runDataService;
        private readonly ParallelOptions parallelOptions;

        public CopyDigitalMediaService(AppSettings appSettings, ILogger<CopyDigitalMediaService> logger, IFileNameService fileNameService, IDateRecognizerService dateRecognizerService, IFileProviderService fileProviderService, IRunDataService runDataService)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            this.fileNameService = fileNameService;
            this.dateRecognizerService = dateRecognizerService;
            this.fileProviderService = fileProviderService;
            this.runDataService = runDataService;
            this.parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = appSettings.MaxDop };
        }

        public async Task AddMetaAndCopy(DirectoryInfo to)
        {
            var froms = appSettings.InputSettings.SourceFolders?.Where(p => !string.IsNullOrEmpty(p)).Select(p => new DirectoryInfo(p)).ToArray();
            if (froms == null || froms.Length == 0)
            {
                logger.LogError("Unable to find sources");
                return;
            }
            foreach (var from in froms)
            {
                await Copy(from, to, IFileProviderService.FileType.Video);
                await Copy(from, to, IFileProviderService.FileType.Picture);
            }
        }

        private async Task Copy(DirectoryInfo from, DirectoryInfo to, IFileProviderService.FileType fileType)
        {
            logger.LogInformation("About to Copy {FileType}(s) from {Source}...", fileType, from.FullName);
            var medias = fileProviderService.GetFiles(from, fileType);
            logger.LogDebug("Found {Count} {FileType}(s) in {From}", medias.Count(), fileType, from);
            runDataService.Add(medias, from, fileType); //TODO remove files with errors
            await medias.ParallelForEachAsync(fileType == IFileProviderService.FileType.Video? CopyOneVideo:CopyOnePicture, to, appSettings.MaxDop);
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
                if (appSettings.InputSettings.ExcludedFiles!= null && appSettings.InputSettings.ExcludedFiles.Contains(fileInfo.Name))
                {
                    logger.LogInformation("Skipping file {File} because it is part of the exclusion list", fileInfo.FullName);
                    return;
                }
                logger.LogTrace("Processing {File}", fileInfo.FullName);
                ImageFile imageFile = null;
                string destination = appSettings.OutputSettings.UnkownFolderName;
                DateTime dateTimeOriginal = DateTime.MinValue;
                DateTime dateInferred = DateTime.MinValue;
                try
                {
                    imageFile = await GetImageFile(fileInfo);
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

                    DateTime folderDate = !dateRecognizerService.Valid(dateInferred) ? dateTimeOriginal : dateInferred;
                    destination = folderDate != DateTime.MinValue ? Path.Combine(appSettings.OutputSettings.PicturesFolderName, fileNameService.MakeDirectoryName(folderDate)) : appSettings.OutputSettings.UnknownDateFolderName;
                }
                catch (NotValidJPEGFileException)
                {
                    destination = appSettings.OutputSettings.InvalidJpegFolderName;
                }
                catch (NotValidImageFileException)
                {
                    logger.LogDebug("NotValidImageFileException encoutered, will not process DateTaken for {File} ", fileInfo.Name);
                    destination = appSettings.OutputSettings.InvalidJpegFolderName;
                }
                catch (IOException e)
                {
                    logger.LogWarning(e, "Unable to get file {File}", fileInfo.Name);
                }

                bool sourceWhatsapp = SourceWhatsApp(fileInfo);
                var targetDirectory = SourceWhatsApp(fileInfo)? 
                                            new DirectoryInfo(Path.Combine(to.FullName, sourceWhatsapp ? appSettings.OutputSettings.WhatsappFolderName : string.Empty, destination)):
                                            new DirectoryInfo(Path.Combine(to.FullName, destination));

                await Copy(fileInfo, targetDirectory, dateInferred);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task<ImageFile> GetImageFile(FileInfo fileInfo, int retryAttemptLeft = 2)
        {
            try
            {
                return await ImageFile.FromFileAsync(fileInfo.FullName);
            }
            catch (IOException e)
            {
                if (e.Message.Contains("cloud", StringComparison.InvariantCultureIgnoreCase))
                {
                    logger.LogTrace(e, "Unable to get image {File} attempts: {Attempt}", fileInfo.FullName, retryAttemptLeft);
                    if (retryAttemptLeft > 0)
                        return await GetImageFile(fileInfo, retryAttemptLeft - 1);
                }
                throw e;
            }
        }

        private async Task Copy(FileInfo fileInfo, DirectoryInfo targetDirectory, DateTime dateInferred)
        {
            if (!targetDirectory.Exists)
            {
                targetDirectory.Create();
                logger.LogDebug("Created {Directory}", targetDirectory.FullName);
            }
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
                    logger.LogDebug(e, "Unable to add date {Date} to file {File}", dateInferred.ToString(), destFileName);
                }
            }
        }

        private bool SourceWhatsApp(FileInfo fi)
        {
            var regex = new Regex(appSettings.WhatsappNameRegex);
            return regex.IsMatch(fi.Name);
        }


        public void PropagateToOtherTargets()
        {
            var targets = appSettings.OutputSettings.TargetDirectories;
            if (targets.Length <= 1)
                return;
            foreach (var target in targets.Skip(1))
            {
                logger.LogInformation("about to copy files and directories from {Source} to {Destination}", targets[0], target);
                CopyAll(targets[0], target);
                logger.LogInformation("Done copying files and directories from {Source} to {Destination}", targets[0], target);
            }
        }

        private void CopyAll(string SourcePath, string DestinationPath)
        {
            var directories = Directory.GetDirectories(SourcePath, appSettings.AllFileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(directories, parallelOptions, directory =>
            {
                try
                {
                    Directory.CreateDirectory(directory.Replace(SourcePath, DestinationPath));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to create directory {Source}", directory);
                }
            });

            var files = Directory.GetFiles(SourcePath, appSettings.AllFileExtensions, SearchOption.AllDirectories);
            Parallel.ForEach(files, parallelOptions, file =>
            {
                try
                {
                    File.Copy(file, file.Replace(SourcePath, DestinationPath));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unable to copy file {Source}", file);
                }
            });
        }
    }
}