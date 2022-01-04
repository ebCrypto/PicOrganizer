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
        List<DirectoryReplace> records;

        public FileNameCleanerService(AppSettings appSettings, ILogger<FileNameCleanerService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
        }

        public void LoadCleanDirList(FileInfo fi)
        {
            if (!fi.Exists)
            {
                logger.LogWarning("Unable to find {File}", fi.FullName);    
                return;
            }
            try
            {
                using var reader = new StreamReader(fi.FullName);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                records = csv.GetRecords<DirectoryReplace>().ToList();
                logger.LogDebug("Found {Count} entries in {File}", records.Count, fi.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to clean names");
            }
        }

        public string CleanName(string input)
        {
            var output = input;
            if (records != null && records.Any() && !string.IsNullOrEmpty(input))
            {
                foreach (var record in records)
                    output = output.Replace(record.Original, record.ReplaceWith);
            }
            return output;
        }

        public string AddParentDirectoryToFileName(FileInfo fileInfo)
        {
            if (fileInfo == null)
                return String.Empty;
            try
            {
                string directoryName = CleanName(fileInfo.Directory?.Name);
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