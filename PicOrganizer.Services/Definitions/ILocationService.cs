using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<ReportDetail>> Report(DirectoryInfo di);
    }
}