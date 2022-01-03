namespace PicOrganizer.Services
{
    public interface ITagService
    {
        public void CreateTags(DirectoryInfo di);
        
        public void AddRelevantTagsToFiles(DirectoryInfo di);
    }
}