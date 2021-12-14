namespace PicOrganizer.Services
{
    public interface ITimelineToFilesService
    {
        public Task AddFloatCoordinatesToImage(string latitude, string longitude, FileInfo fi);

        public void LoadTimeLine(FileInfo csv);

        public Task AddlocationFromTimeLine(FileInfo fi);

        public Task AddlocationFromTimeLine(DirectoryInfo di);
    }
}