using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface IReportReadWriteService
    {
        Task WriteAsync<T>(FileInfo fileInfo, List<T> records);
        void Write<T>(FileInfo fileInfo, List<T> records);
        Task<IEnumerable<T>> Read<T>(FileInfo fileInfo);
    }
}