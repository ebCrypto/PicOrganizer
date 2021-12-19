using CsvHelper;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Globalization;

namespace PicOrganizer.Services
{
    public class FileNameCleanerService : IFileNameCleanerService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<FileNameCleanerService> logger;
        readonly List<DirectoryReplace> records;

        public FileNameCleanerService(AppSettings appSettings, ILogger<FileNameCleanerService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
            using var reader = new StreamReader("Data\\CleanDirectoryName.csv");
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            records = csv.GetRecords<DirectoryReplace>().ToList();
        }

        public string CleanNameUsingParentDir(FileInfo fileInfo)
        {
            try
            {
                string directoryName = fileInfo.Directory.Name;
                var replaces = records.Where(p => directoryName.Contains(p.Original)).ToList();
                if (replaces.Any() && !string.IsNullOrEmpty(directoryName))
                    foreach (var replace in replaces)
                        directoryName = directoryName.Replace(replace.Original, replace.ReplaceWith);
                directoryName += "_";
                var fileName = fileInfo.Name;
                if (fileName.Length > 20)
                {
                    fileName = fileName.Substring(0, 20) + fileInfo.Extension;
                    logger.LogDebug("Using First 20 char of {File}", fileName);
                }
                string result = string.Format($"{directoryName}{fileName}").Replace("__", "_");
                if (result.StartsWith("_"))
                    result = result.Substring(1);
                return result;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Can't make name {Name}", fileInfo.FullName);
                return Guid.NewGuid().GetHashCode().ToString() + fileInfo.Extension;
            }
        }
    }
}
