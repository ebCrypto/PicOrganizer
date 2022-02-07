using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Text.Json;

namespace PicOrganizer.Services
{
    public class MetaDataService : IMetaDataService
    {
        private readonly ILogger<MetaDataService> logger;
        private readonly AppSettings appSettings;
        private readonly IFileProviderService fileProviderService;

        public MetaDataRun metaDataRun { get; set; }

        public MetaDataService(ILogger<MetaDataService> logger, AppSettings appSettings, IFileProviderService fileProviderService)
        {
            this.logger = logger;
            this.appSettings = appSettings;
            this.fileProviderService = fileProviderService;
            metaDataRun = new MetaDataRun()
            {
                Id = Guid.NewGuid(),
                startTime = DateTimeOffset.Now,
                Folders = new Dictionary<string, MetaDataFolder>()
            };
        }

        public async Task ReadFromDisk(DirectoryInfo source)
        {
            var metaFile = source.GetFiles("*.json", SearchOption.TopDirectoryOnly).OrderByDescending(p => p.LastWriteTime).FirstOrDefault();
            if (metaFile == null)
                logger.LogWarning("Unable to find a json file in the directory {Path}", source.FullName);
            else
                try
                {
                    var data = await File.ReadAllTextAsync(metaFile.FullName);
                    metaDataRun = JsonSerializer.Deserialize<MetaDataRun>(data);
                    metaDataRun.Id = Guid.NewGuid();
                    metaDataRun.startTime = DateTimeOffset.Now;
                    fileProviderService.SetProcessedPreviously(metaDataRun.Folders.SelectMany(p => p.Value.Files.Select(q => q.FullName)).ToList());
                    logger.LogInformation("using meta found in {File}", metaFile.FullName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Unable to load meta file {Path}", metaFile.FullName);
                }
        }

        public void WriteToDisk(DirectoryInfo target)
        {
            try
            {
                var directory = new DirectoryInfo(Path.Combine(target.FullName, appSettings.OutputSettings.MetaDataFolderName));
                Directory.CreateDirectory(directory.FullName);
                var picturesInTarget = fileProviderService.GetFiles(target, IFileProviderService.FileType.Picture, true);
                Add(picturesInTarget, target, IFileProviderService.FileType.Picture);
                var videosInTarget = fileProviderService.GetFiles(target, IFileProviderService.FileType.Video, true);
                Add(videosInTarget, target, IFileProviderService.FileType.Video);
                metaDataRun.endTime = DateTimeOffset.Now;
                string path = Path.Combine(directory.FullName, metaDataRun.endTime.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".json");
                File.WriteAllText(path, JsonSerializer.Serialize(metaDataRun));
                logger.LogInformation("Saved Meta {File}", path);
            }
            catch (Exception e)
            {
                logger.LogError(e, "unable to save metaData");
            }
        }

        public void Add(IEnumerable<FileInfo> result, DirectoryInfo di, IFileProviderService.FileType fileType)
        {
            if (!result.Any())
                return;
            MetaDataFolder folder = new()
            {
                Name = di.Name,
                FullName = di.FullName,
                Files = result.Where(q => q != null).Select(
                                        p => new MetaDataFile(p)
                                    )
            };
            if (!metaDataRun.Folders.ContainsKey(di.FullName))
                metaDataRun.Folders.Add(di.FullName, folder);
            else
                metaDataRun.Folders[di.FullName].Files = metaDataRun.Folders[di.FullName].Files.Union(folder.Files).Distinct();
        }
    }
}