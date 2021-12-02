using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface IReportWriterService
    {
        Task Write<T>(FileInfo fileInfo, List<T> records);
    }
}