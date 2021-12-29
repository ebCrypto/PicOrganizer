using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ITimelineToFilesService
    {

        public void LoadTimeLine(FileInfo csv);
        List<ReportMissingLocation> GetTimeline();
    }
}