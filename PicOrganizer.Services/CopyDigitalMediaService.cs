using ExifLibrary;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public class CopyDigitalMediaService : ICopyDigitalMediaService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<CopyDigitalMediaService> _logger;
        private readonly IDirectoryNameService _directoryNameService;
        private readonly IFileNameCleanerService _fileNameCleanerService;
        private readonly string[] extensions;

        public CopyDigitalMediaService(AppSettings appSettings, ILogger<CopyDigitalMediaService> logger, IDirectoryNameService directoryNameService, IFileNameCleanerService fileNameCleanerService)
        {
            this.appSettings = appSettings;
            _logger = logger;
            _directoryNameService = directoryNameService;
            _fileNameCleanerService = fileNameCleanerService;
            extensions = appSettings.VideoAndPhotoExtensions;
        }

        public async Task Copy(DirectoryInfo from, DirectoryInfo to)
        {
            _logger.LogInformation(@"Processing {Source}", from.FullName);
            await from.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories)
                .Where(p => extensions.Contains(p.Extension.ToLower()))
                .ParallelForEachAsync<FileInfo, DirectoryInfo>(CopyOne, to);
        }

        private async Task CopyOne(FileInfo fileInfo, DirectoryInfo to)
        {
            try
            {
                _logger.LogTrace("Processing {File}", fileInfo.FullName);
                ImageFile imageFile;
                DateTime dateTimeOriginal = DateTime.MinValue;
                string? destination = appSettings.UnkownFolderName;
                DateTime dateInferred = DateTime.MinValue;
                try
                {
                    imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    ExifProperty? tag;
                    tag = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                    _ = DateTime.TryParse(tag?.ToString(), out dateTimeOriginal);

                    if (dateTimeOriginal == DateTime.MinValue)
                        dateInferred = await InferDateFromName(fileInfo.Name);
                    if (dateTimeOriginal == DateTime.MinValue && dateInferred == DateTime.MinValue)
                        dateInferred = await InferDateFromName(fileInfo.Directory.Name);
                    if (dateInferred == DateTime.MinValue)
                        destination = _directoryNameService.GetName(dateTimeOriginal);
                    else
                        destination = _directoryNameService.GetName(dateInferred);
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

                var targetDirectory = new DirectoryInfo(Path.Combine(to.FullName, destination));
                if (!targetDirectory.Exists)
                {
                    targetDirectory.Create();
                    _logger.LogDebug("Created {Directory}", targetDirectory.FullName);
                }

                await Copy(fileInfo, targetDirectory, dateInferred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }

        private async Task Copy(FileInfo fileInfo, DirectoryInfo targetDirectory, DateTime dateInferred)
        {
            string cleanName = _fileNameCleanerService.CleanNameUsingParentDir(fileInfo);
            fileInfo.CopyTo(Path.Combine(targetDirectory.FullName, cleanName), true);
            if (dateInferred != DateTime.MinValue)
            {
                try
                {
                    var imageFile = await ImageFile.FromFileAsync(fileInfo.FullName);
                    imageFile.Properties.Set(ExifTag.DateTimeOriginal, dateInferred);
                    await imageFile.SaveAsync(fileInfo.FullName);
                    _logger.LogDebug("Added date {Date} to file {File}", dateInferred.ToString(), fileInfo.FullName);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Unable to add date {Date} to file {File}", dateInferred.ToString(), fileInfo.FullName);
                }
            }
        }

        private async Task<DateTime> InferDateFromName(string name)
        {
            List<ModelResult>? modelResults = DateTimeRecognizer.RecognizeDateTime(name, Culture.English);
            if (modelResults.Any())
            {
                foreach (var modelResult in modelResults)
                {
                    SortedDictionary<string, object>? resolution = modelResult.Resolution;
                    _logger.LogDebug("Found {Count} item(s) in date resolution for name {Name}", resolution.Count(), name);
                    foreach (KeyValuePair<string, object> resolutionValue in resolution)
                    {
                        var value = (List<Dictionary<string, string>>)resolutionValue.Value;
                        _logger.LogTrace("Found {Count} value(s) in this resolution for name {Name}", value.Count, name);
                        DateTime.TryParse(value?[0]?["timex"], out var result);
                        if (result.Year > appSettings.StartingYearOfLibrary && result < DateTime.Now)
                        {
                            _logger.LogInformation("Inferring DateTaken '{Date}' from name {Name}", result.ToString(), name);
                            return result;
                        }
                    }
                }
            }
            else
            {
                if (name.StartsWith("IMG-") || name.StartsWith("IMG_"))
                    return await InferDateFromName(string.Format($"{name.Substring(4, 4)}-{name.Substring(8, 2)}-{name.Substring(10, 2)}"));
                if (name.StartsWith("VZM.IMG_"))
                    return await InferDateFromName(string.Format($"{name.Substring(8, 4)}-{name.Substring(12, 2)}-{name.Substring(14, 2)}"));
            }

            return DateTime.MinValue;
        } 
    }
}