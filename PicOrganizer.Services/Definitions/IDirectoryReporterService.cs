using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface IDirectoryReporterService
    {
        Task<IEnumerable<ReportDetail>> LocationReport(DirectoryInfo di);
    }
}