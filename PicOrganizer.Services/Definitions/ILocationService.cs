using CoordinateSharp;
using ExifLibrary;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        IEnumerable<ReportDetail> ReportMissing(DirectoryInfo di, LocationWriter lw, bool writeToDisk = true);
        Task WriteLocation(DirectoryInfo di, LocationWriter lw);
        Task SaveCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        Task SaveDMSCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        void MakeLongitude(Coordinate coordinate, ImageFile ef);
        void MakeLatitude(Coordinate coordinate, ImageFile ef);
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