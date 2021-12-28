using CoordinateSharp;
using ExifLibrary;
using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface ILocationService
    {
        Task<IEnumerable<ReportDetail>> ReportMissing(DirectoryInfo di);
        Task WriteLocationFromClosestKnownIfSameDay(FileInfo reportDetails);
        Task WriteLocationFromClosestKnownIfSameDay(DirectoryInfo di);
        public Task SaveDoubleCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        public Task SaveDMSCoordinatesToImage(string latitude, string longitude, FileInfo fi);
        public void MakeLongitude(Coordinate coordinate, ImageFile ef);

        public void MakeLatitude(Coordinate coordinate, ImageFile ef);
    }
}