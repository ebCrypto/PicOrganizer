namespace PicOrganizer.Models
{
    public class AppSettings
    {
        public AppSettings()
        {
            //TODO move these to appSettings.json
            UnkownFolderName = "unknown";
            DuplicatesFolderName = "duplicates";
            VideosFolderName = "Videos";
            InvalidJpegFolderName = "InvalidJpeg";
            AllFileExtensions = "*.*";
            PictureExtensions = new[] { ".jpeg", ".jpg", ".png", ".bmp", ".tiff" };
            VideoExtensions = new[] { ".avi", ".mpg", ".mpeg", ".mp4", ".mov", ".wmv", ".mkv" };
            StartingYearOfLibrary = 1970;
            ReportDuplicatesName = "reportDuplicates.csv";
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
        }

        public string VideosFolderName { get; set; }
        public string DuplicatesFolderName { get; set; }
        public string AllFileExtensions { get; set; }
        public string PictureFilter { get { return string.Join("|", PictureExtensions.Select(p => string.Format($"*{p}"))); } }
        public string[] PictureExtensions { get; set; }
        public string[] VideoExtensions { get; set; }
        public string InvalidJpegFolderName { get; set; }
        public int StartingYearOfLibrary { get; set; }
        public string UnkownFolderName { get; set; }
        public string ReportDuplicatesName { get; set; }
        public string[] KnownUsedNameFormats { get; set; }
    }
}
