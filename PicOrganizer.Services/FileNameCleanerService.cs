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
            try
            {
                using var reader = new StreamReader("Data\\CleanDirectoryName.csv");
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                records = csv.GetRecords<DirectoryReplace>().ToList();
                logger.LogDebug("Found {Count} entries in CleanDirectoryName.csv", records.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to clean names");
            }
        }

        public string CleanName(string input)
        {
            var output = input;
            if (records != null && records.Any())
            {
                var replaces = records.Where(p => input.Contains(p.Original)).ToList();
                if (replaces.Any() && !string.IsNullOrEmpty(input))
                    foreach (var replace in replaces)
                        output = input.Replace(replace.Original, replace.ReplaceWith);
            }
            return output;
        }

        public string AddParentDirectoryToFileName(FileInfo fileInfo)
        {
            try
            {
                string directoryName = CleanName(fileInfo.Directory.Name);
                if (directoryName.Length > 0)
                    directoryName += " ";
                var fileName = fileInfo.Name;
                if (Guid.TryParse(fileName[^(fileInfo.Extension.Length)].ToString(), out var resultGuid))
                {
                    logger.LogDebug("Found Guid {File}", fileName);
                    fileName = Math.Abs(fileName.GetHashCode()) + fileInfo.Extension;
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