using CoordinateSharp;
using ExifLibrary;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<ReportDetail>> ReportMissing(DirectoryInfo di, string step);
        Task WriteLocation(DirectoryInfo di, LocationWriter lw);
        Task SaveDoubleCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        Task SaveDMSCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        void MakeLongitude(Coordinate coordinate, ImageFile ef);
        void MakeLatitude(Coordinate coordinate, ImageFile ef);

        enum LocationWriter
        {
            FromClosestSameDay = 1, // TODO binary mask and break into 2 options?
            FromTimeline = 2
        }
    }
}