using CsvHelper;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Globalization;

namespace PicOrganizer.Services
{
    public class CsvReportWriterService : IReportWriterService
    {
        private readonly ILogger<CsvReportWriterService> logger;

        public CsvReportWriterService(ILogger<CsvReportWriterService> logger)
        {
            this.logger = logger;
        }

        public async Task Write<T>(FileInfo fileInfo, List<T> records)
        {
            if (records != null && records.Any())
            {
                using var writer = new StreamWriter(fileInfo.FullName, false);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                logger.LogDebug("Writing {Count} to {FileName}", records.Count, fileInfo.FullName);
                await csv.WriteRecordsAsync(records);
            }
            else
                logger.LogDebug("Nothing to write to {FileName}", fileInfo.FullName);

        }
    }
}
