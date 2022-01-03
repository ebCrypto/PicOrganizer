using System.Runtime.Serialization;

namespace PicOrganizer.Models
{
    public class AppSettings
    {
        public AppSettings()
        {
            //TODO move these to appSettings.json
            UnkownFolderName = "unknown";
            MaxDop = 4;
            DuplicatesFolderName = "duplicates";
            WhatsappFolderName = "from-family";
            VideosFolderName = "Videos";
            InvalidJpegFolderName = "InvalidJpeg";
            PictureExtensions = new[] { ".jpeg", ".jpg", ".png", ".bmp", ".tiff" };
            VideoExtensions = new[] { ".avi", ".mpg", ".mpeg", ".mp4", ".mov", ".wmv", ".mkv" };
            StartingYearOfLibrary = 1970;
            ReportDuplicatesName = "reportDuplicates.csv";
            ReportDetailName = "reportDetail.csv";
            KnownUsedNameFormats = new string[] { 
                "yyyy-MM-dd-HH-mm-ss", 
                "yyyy-MM-dd_HH-mm-ss", 
                "yyyyMMdd_HHmmss-", 
                "yyyyMMddHHmm" ,
                "yyyyMMdd_HHmmss",
                "yyyyMMdd", 
                "MMddyyHHmm",
                "_yyyy-MM-dd-HH-mm-ss", 
                "_yyyyMMdd_HHmmss", 
                "_yyyyMMdd_HHmmss_ff", 
                "_yyyyMMdd", 
                "-yyyyMMdd", 
            };
            TagSkipper = new[] { "the", "a", "with", "or", "and", "for", "pre", "vzm", "img", "lg", "samsung", "cannon", "dropbox"};
            WhatsappNameRegex = @"IMG-[0-9]{8}-WA[0-9]{4}";
            Scanned = "scanned";
            AllFileExtensions = "*";
            ExcludedFiles = new[] { "IMG_2915.JPG", "IMG_3676.JPEG", "IMG_3677.JPEG", "IMG_3678.JPEG", "IMG_3679.JPEG", "IMG_3680.JPEG", "IMG_3681.JPEG", };
        }

        public string VideosFolderName { get; set; }
        public string DuplicatesFolderName { get; set; }
        public string PictureFilter { get { return string.Join("|", PictureExtensions.Select(p => string.Format($"*{p}"))); } }
        public string PictureAndVideoFilter { get { return string.Join("|", PictureExtensions.Union(VideoExtensions).Select(p => string.Format($"*{p}"))); } }
        public string[] PictureExtensions { get; set; }
        public string[] VideoExtensions { get; set; }
        public string InvalidJpegFolderName { get; set; }
        public int StartingYearOfLibrary { get; set; }
        public string UnkownFolderName { get; set; }
        public string ReportDuplicatesName { get; set; }
        public string[] KnownUsedNameFormats { get; set; }
        public int MaxDop { get; set; }
        public string[] TagSkipper { get; set; }
        public string WhatsappNameRegex { get; set; }
        public string Scanned { get; set; }
        public string WhatsappFolderName { get; set;  }
        public string AllFileExtensions { get; set; }
        public string[] ExcludedFiles { get; }
        public string ReportDetailName { get; set; }
    }
}
