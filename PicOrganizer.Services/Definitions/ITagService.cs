namespace PicOrganizer.Services
{
    public interface ITagService
    {
        void CreateTags(DirectoryInfo di);
        
        void AddRelevantTagsToFiles(DirectoryInfo di);
    }
}