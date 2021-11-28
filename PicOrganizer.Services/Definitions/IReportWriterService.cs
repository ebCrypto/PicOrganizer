using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface IReportWriterService
    {
        Task Write(FileInfo fileInfo, List<ReportDetail> records);
    }
}