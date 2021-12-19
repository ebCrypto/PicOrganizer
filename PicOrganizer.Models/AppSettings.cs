using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Models
{
    public class AppSettings
    {
        public AppSettings()
        {
            DuplicatesFolderName = "duplicates";
            VideosFolderName = "Videos";
            InvalidJpegFolderName = "InvalidJpeg";
            VideoAndPhotoExtensions = new[] { ".jpeg", ".jpg", ".avi", ".mpg", ".mpeg", ".mp4", ".mov", ".wmv", ".mkv", ".png" };
            AllFileExtensions = "*.*";
            PictureExtensions = "*.jpeg|*.jpg|*.png|*.bmp|*.tiff";
            JpegExtension = "*.jpeg";
            StartingYearOfLibrary = 2004;
        }

        public string VideosFolderName { get; set; }

        public string DuplicatesFolderName { get; set; }
        public string[] VideoAndPhotoExtensions { get; set; }
        public string AllFileExtensions { get; set; }
        public string PictureExtensions { get; set; }
        public string JpegExtension { get; set; }
        public string? InvalidJpegFolderName { get; set; }
        public int StartingYearOfLibrary { get; set; }
    }
}
