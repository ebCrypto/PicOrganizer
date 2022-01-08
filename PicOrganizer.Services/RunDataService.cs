using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Text.Json;

namespace PicOrganizer.Services
{
    public class RunDataService : IRunDataService
    {
        private readonly ILogger<RunDataService> logger;
        private readonly AppSettings appSettings;
        private readonly IFileProviderService fileProviderService;

        public MetaDataRun metaDataRun { get; set; }

        public RunDataService(ILogger<RunDataService> logger, AppSettings appSettings, IFileProviderService fileProviderService)
        {
            this.logger = logger;
            this.appSettings = appSettings;
            this.fileProviderService = fileProviderService;
            metaDataRun = new MetaDataRun()
            {
                Id = Guid.NewGuid(),
                startTime = DateTimeOffset.Now,
                Folders = new List<MetaDataFolder>()
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
                    fileProviderService.SetExceptionList(metaDataRun.Folders.SelectMany(p => p.Files.Select(q => q.FullName)).ToList());
                    logger.LogInformation("using meta found in {File}", metaFile.FullName);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Unable to load file {Path}", metaFile.FullName);
                }
        }

        public void WriteToDisk(DirectoryInfo target)
        {
            var directory = new DirectoryInfo(Path.Combine(target.FullName , appSettings.OutputSettings.MetaDataFolderName));
            Directory.CreateDirectory(directory.FullName);
            var filesInTarget = fileProviderService.GetFiles(target, IFileProviderService.FileType.AllMedia);
            Add(filesInTarget, target, IFileProviderService.FileType.AllMedia);  
            metaDataRun.endTime = DateTimeOffset.Now;
            string path = Path.Combine(directory.FullName, metaDataRun.endTime.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(metaDataRun));
            logger.LogInformation("Saved Meta {File}", path);
        }

        public void Add(IEnumerable<FileInfo> result, DirectoryInfo di, IFileProviderService.FileType fileType)
        {
            metaDataRun.Folders.Add (
            new MetaDataFolder() {
                Name = di.Name,
                FullName = di.FullName,
                Type = fileType.ToString(),
                Files = result.Select (
                    p=>new MetaDataFile() { 
                    FullName = p.FullName, 
                    Name = p.Name, 
                    Extension = p.Extension, 
                    LastWriteTimeUtc = p.LastWriteTimeUtc,
                    Length = p.Length
                    }
                )
            });
        } 
    }
}
