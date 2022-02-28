using CsvHelper;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Globalization;
using System.Text.RegularExpressions;

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
            if (dt.Year < appSettings.InputSettings.StartingYearOfLibrary)
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
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            var extension = Path.GetExtension(input);
            var output = /*RemoveLeadingZeros(*/Regex.Replace(string.IsNullOrEmpty(extension) ? input : input[..^extension.Length], @"\s+", " ")/*)*/;
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
        private static string RemoveLeadingZeros(string str)
        {

            // Regex to remove leading
            // zeros from a string
            string regex = "^0+(?!$)";

            // Replaces the matched
            // value with given string
            str = Regex.Replace(str, regex, "");

            return str;
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
                string result = string.Format($"{cleanFolderName}{cleanFileName}");
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