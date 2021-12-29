using CoordinateSharp;
using ExifLibrary;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<ReportDetail>> ReportMissing(DirectoryInfo di, string step);
        Task WriteLocation(DirectoryInfo di, LocationWriter lw);
        public Task SaveDoubleCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        public Task SaveDMSCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        public void MakeLongitude(Coordinate coordinate, ImageFile ef);
        public void MakeLatitude(Coordinate coordinate, ImageFile ef);

        public enum LocationWriter
        {
            FromClosestSameDay = 1, // TODO binary mask and break into 2 options?
            FromTimeline = 2
        }
    }
}