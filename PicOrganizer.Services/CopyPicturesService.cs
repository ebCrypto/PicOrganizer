using ExifLibrary;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public class CopyPicturesService : ICopyPicturesService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<CopyPicturesService> _logger;
        private readonly IDirectoryNameService _directoryNameService;
        private readonly IFileNameCleanerService _fileNameCleanerService;
        private readonly string[] extensions;

        public CopyPicturesService(Models.AppSettings appSettings, ILogger<CopyPicturesService> logger, IDirectoryNameService directoryNameService, IFileNameCleanerService fileNameCleanerService)
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
            //var tasks = from.GetFiles("*.*", SearchOption.AllDirectories).Where(p=> extensions.Contains(p.Extension.ToLower() )).Select(f => CopyOne(f, to)).ToList();
            //await Task.WhenAll(tasks);

            await from.GetFiles(appSettings.AllFileExtensions, SearchOption.AllDirectories).Where(p => extensions.Contains(p.Extension.ToLower())).ParallelForEachAsync<FileInfo, DirectoryInfo>(CopyOne, to);
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

                    if (dateTimeOriginal == DateTime.MinValue)
                        InferDateFromDate(fileInfo.Name, dateTimeOriginal);

                    destination = _directoryNameService.GetName(dateTimeOriginal);
                }
                catch (NotValidJPEGFileException)
                {
                    destination = appSettings.InvalidJpegFolderName;
                }
                catch (NotValidImageFileException)
                {
                    destination = appSettings.VideosFolderName;
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

        private void InferDateFromDate(string name, DateTime dateTimeOriginal)
        {
            List<ModelResult>? modelResults = DateTimeRecognizer.RecognizeDateTime(name, Culture.English);
            if (modelResults.Any())
            {
                foreach (var modelResult in modelResults)
                {
                    SortedDictionary<string, object>? resolution = modelResult.Resolution;
                    _logger.LogDebug("Found {Count} item(s) in date resolution for file {Name}", resolution.Count(), name);
                    foreach (KeyValuePair<string, object> resolutionValue in resolution)
                    {
                        var value = (List<Dictionary<String, String>>)resolutionValue.Value;
                        _logger.LogDebug("Found {Count} value(s) in this resolution for file {Name}", value.Count, name);
                        DateTime.TryParse(value?[0]?["timex"], out var result);
                        if (result.Year > 2004)
                        {
                            dateTimeOriginal = result;
                            _logger.LogInformation("Inferring DateTaken '{Date}' from file name {Name}", result.ToString(), name);
                            return;
                        }
                    }
                }
            }
            else
            {
                if (name.StartsWith("IMG-") || name.StartsWith("IMG_"))
                    InferDateFromDate(name.Substring(4,8).Replace("_", " "), dateTimeOriginal);
            }
        }
    }
}
