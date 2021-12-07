using CsvHelper;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Services
{
    public class FileNameCleanerService : IFileNameCleanerService
    {
        private readonly ILogger<FileNameCleanerService> logger;
        List<DirectoryReplace> records;

        public FileNameCleanerService(ILogger<FileNameCleanerService> logger)
        {
            this.logger = logger;
            using var reader = new StreamReader("Data\\CleanDirectoryName.csv");
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            records = csv.GetRecords<DirectoryReplace>().ToList();
        }

        public string MakeDirectoryName(FileInfo fileInfo)
        {
            string directoryName = fileInfo.Directory.Name;
            var replaces = records.Where(p => directoryName.Contains(p.Original)).ToList();
            if (replaces.Any() && !string.IsNullOrEmpty(directoryName))
                foreach (var replace in replaces)
                directoryName = directoryName.Replace(replace.Original, replace.ReplaceWith);
            directoryName += "_";
            return String.Format($"{directoryName}{fileInfo.Name}");
        }
    }
}
