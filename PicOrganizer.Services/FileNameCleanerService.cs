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
            if (records.Any(p => p.Original == directoryName))
                directoryName = records.FirstOrDefault(p => p.Original == directoryName).ReplaceWith;
            return String.Format($"{directoryName}_{fileInfo.Name}");
        }

    }
}
