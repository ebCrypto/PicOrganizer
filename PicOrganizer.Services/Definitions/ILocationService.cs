using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        IEnumerable<ReportDetail> ReportMissing(DirectoryInfo di, LocationWriter lw, bool writeToDisk = true);
        Task WriteLocation(DirectoryInfo di, LocationWriter lw);
        void LoadTimeLine(FileInfo csv);
        void LoadKnownLocations(FileInfo csv);
        
        List<ReportMissingLocation> GetTimeline();

        enum LocationWriter
        {
            FromClosestSameDay = 1, // TODO binary mask and break into 2 options?
            FromTimeline = 2, 
            FromFileName = 3,
            Before = 4,
            After = 5,
        }
    }
}