namespace PicOrganizer.Services
{
    public interface IFileNameService
    {
        string AddParentDirectoryToFileName(FileInfo fileInfo);
        string CleanName(string input);
        void LoadCleanDirList(FileInfo fi);
        string MakeDirectoryName(DateTime dt);
    }
}