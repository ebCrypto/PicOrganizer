using CsvHelper;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace PicOrganizer.Services
{
    public class CsvReadWriteService : IReportReadWriteService
    {
        private readonly ILogger<CsvReadWriteService> logger;

        public CsvReadWriteService(ILogger<CsvReadWriteService> logger)
        {
            this.logger = logger;
        }

        public async Task WriteAsync<T>(FileInfo fileInfo, List<T> records)
        {
            if (records != null && records.Any())
            {
                using var writer = new StreamWriter(fileInfo.FullName, false);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                logger.LogDebug("Writing {Count} row(s) to {FileName}", records.Count, fileInfo.FullName);
                await csv.WriteRecordsAsync(records);
            }
            else
                logger.LogTrace("Nothing to write to {FileName}", fileInfo.FullName);
        }

        public void Write<T>(FileInfo fileInfo, List<T> records)
        {
            if (records != null && records.Any())
            {
                using var writer = new StreamWriter(fileInfo.FullName, false);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                logger.LogDebug("Writing {Count} row(s) to {FileName}", records.Count, fileInfo.FullName);
                csv.WriteRecords(records);
            }
            else
                logger.LogTrace("Nothing to write to {FileName}", fileInfo.FullName);
        }

        public async Task<IEnumerable<T>> Read<T>(FileInfo fileInfo)
        {
            using var reader = new StreamReader(fileInfo.FullName);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<T>().ToList();
            logger.LogDebug("Read {Count} from {FileName}", records.Count(), fileInfo.FullName);
            return records;
        }
    }
}
