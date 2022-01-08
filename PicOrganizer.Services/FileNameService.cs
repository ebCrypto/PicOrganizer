using CsvHelper;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Globalization;

namespace PicOrganizer.Services
{
    public class FileNameService : IFileNameService
    {
        private readonly AppSettings appSettings;
        private readonly ILogger<FileNameService> logger;
        List<DirectoryReplace> records;

        public FileNameService(AppSettings appSettings, ILogger<FileNameService> logger)
        {
            this.appSettings = appSettings;
            this.logger = logger;
        }

        public string MakeDirectoryName(DateTime dt)
        {
            if (dt.Year <= appSettings.InputSettings.StartingYearOfLibrary)
                return appSettings.OutputSettings.UnknownDateFolderName;
            return dt.ToString(appSettings.OutputSettings.SubFolderDateFormat);
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

            var extension = Path.GetExtension(input);
            var output = string.IsNullOrEmpty(extension) ? input : input[..^extension.Length];

            if (Guid.TryParse(output, out var resultGuid))
            {
                output = Math.Abs(output.GetHashCode()).ToString().Substring(1);
                logger.LogDebug("Found Guid in {File}, renaming {NewName}", input, output);
            }
            else
            if (records != null && records.Any() && !string.IsNullOrEmpty(input))
            {
                foreach (var record in records)
                    output = output.Replace(record.Original, record.ReplaceWith);
            }
            return output + extension;
        }

        public string AddParentDirectoryToFileName(FileInfo fileInfo)
        {
            if (fileInfo == null)
                return String.Empty;
            try
            {
                string cleanFolderName = CleanName(fileInfo.Directory?.Name);
                if (cleanFolderName.Length > 0)
                    cleanFolderName += " ";
                var cleanFileName = CleanName(fileInfo.Name);
                string result = string.Format($"{cleanFolderName}{cleanFileName}").Replace("__", "_");
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