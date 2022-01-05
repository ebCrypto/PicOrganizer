namespace PicOrganizer.Services
{
    public interface IFileNameService
    {
        public string AddParentDirectoryToFileName(FileInfo fileInfo);
        public string CleanName(string input);
        public void LoadCleanDirList(FileInfo fi);
        string MakeDirectoryName(DateTime dt);
    }
}