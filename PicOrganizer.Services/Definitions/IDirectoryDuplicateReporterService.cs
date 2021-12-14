namespace PicOrganizer.Services
{
    public interface IDirectoryDuplicateReporterService
    {
        Task ReportAndMoveDuplicates(DirectoryInfo di, DirectoryInfo destination);
    }
}