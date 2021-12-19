namespace PicOrganizer.Services
{
    public interface IFileNameCleanerService
    {
        public string CleanNameUsingParentDir(FileInfo fileInfo);
    }
}