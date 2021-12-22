namespace PicOrganizer.Services
{
    public interface IFileNameCleanerService
    {
        public string AddParentDirectoryToFileName(FileInfo fileInfo);
        public string CleanName(string input);
    }
}