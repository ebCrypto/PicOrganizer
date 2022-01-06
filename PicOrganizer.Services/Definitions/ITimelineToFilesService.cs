using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ITimelineToFilesService
    {
        void LoadTimeLine(FileInfo csv);
        List<ReportMissingLocation> GetTimeline();
    }
}