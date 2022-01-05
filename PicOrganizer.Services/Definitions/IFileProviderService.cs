namespace PicOrganizer.Services
{
    public interface IFileProviderService
    {
        IEnumerable<FileInfo> GetFiles(DirectoryInfo di, FileType fileType);

        public enum FileType
        {
            All, 
            AllMedia,
            Video,
            Picture
        }

        IEnumerable<FileInfo> GetFilesViaPattern(DirectoryInfo source, string searchPatterns, SearchOption searchOption);
        public void SetExcept(IEnumerable<FileInfo> except);
    }
}