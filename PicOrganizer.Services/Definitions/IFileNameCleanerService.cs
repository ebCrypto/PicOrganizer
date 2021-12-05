namespace PicOrganizer.Services
{
    public interface IFileNameCleanerService
    {
        public string MakeDirectoryName(FileInfo fileInfo);
    }
}