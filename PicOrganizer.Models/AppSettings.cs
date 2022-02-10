namespace PicOrganizer.Models
{
    public class AppSettings
    {
        public AppSettings()
        {
            InputSettings = new Inputs();
            OutputSettings = new Outputs();
        }

        public string VideoFilter { get { return VideoExtensions == null ? string.Empty : string.Join("|", VideoExtensions.Select(p => string.Format($"*{p}"))); } }
        public string PictureFilter { get { return PictureExtensions == null ? string.Empty : string.Join("|", PictureExtensions.Select(p => string.Format($"*{p}"))); } }
        public string PictureAndVideoFilter { get { return PictureExtensions == null ? string.Empty : string.Join("|", PictureExtensions.Union(VideoExtensions).Select(p => string.Format($"*{p}"))); } }
        public string[] PictureExtensions { get; set; }
        public string[] VideoExtensions { get; set; }
        public string[] KnownUsedDateFormatsInNames { get; set; }
        public int MaxDop { get; set; }
        public string[] TagSkipper { get; set; }
        public string WhatsappNameRegex { get; set; }
        public string AllFileExtensions { get; set; }

        public Inputs InputSettings { get; set; }
        public Outputs OutputSettings { get; set; }

        public class Outputs
        {
            public string[] TargetDirectories { get; set; }
            public string VideosFolderName { get; set; }
            public string ReportsFolderName { get; set; }
            public string PicturesFolderName { get; set; }
            public string DuplicatesFolderName { get; set; }
            public bool DeleteDuplicates { get; set; }
            public string MetaDataFolderName { get; set ; }
            public string InvalidJpegFolderName { get; set; }
            public string UnknownDateFolderName { get; set; }
            public string UnkownFolderName { get; set; }
            public string ReportDuplicatesName { get; set; }
            public string ReportDetailName { get; set; }
            public string WhatsappFolderName { get; set; }
            public string SubFolderDateFormat { get; set; }
            public string ReportMissingLocName { get; set; }
            public string InputBackupFolderName { get; set;  }
            public string Md5s { get; set; }
        }

        public class Inputs
        {
            public string[] SourceFolders { get; set; }
            public string CleanDirectoryName { get; set; }
            public string TimelineName { get; set; }
            public string KnownLocations { get; set;  }
            public int StartingYearOfLibrary { get; set; }
            public string[] ExcludedFiles { get; set; }
            public string Scanned { get; set; }
            public Mode Mode { get; set; } 
            public string Culture { get; set; }
            public int RetryAttempts { get; set; }
        }

        public enum Mode
        {
            Full = 0,
            DeltasOnly = 1
        }
    }
}