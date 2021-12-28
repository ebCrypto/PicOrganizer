namespace PicOrganizer.Services
{
    public interface ITimelineToFilesService
    {

        public void LoadTimeLine(FileInfo csv);

        public Task AddlocationFromTimeLine(FileInfo fi);

        public Task AddlocationFromTimeLine(DirectoryInfo di);
    }
}