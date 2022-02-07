namespace PicOrganizer.Services
{
    public interface IFileProviderService
    {
        IEnumerable<FileInfo> GetFiles(DirectoryInfo di, FileType fileType, bool getAlreadyProcessed);

        enum FileType
        {
            All,
            AllMedia,
            Video,
            Picture
        }

        IEnumerable<FileInfo> GetFilesViaPattern(DirectoryInfo source, string searchPatterns, SearchOption searchOption, bool getAlreadyProcessed);

        void SetProcessedPreviously(List<string> except);
    }
}