namespace PicOrganizer.Services
{
    public interface IDirectoryReporterService
    {
        Task Report(DirectoryInfo di);
    }
}