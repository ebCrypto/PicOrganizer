namespace PicOrganizer.Models
{
    public class ReportMissingLocation
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string MissingAddress { get; set; }
        public string MissingLatitude { get; set; }
        public string MissingLongitude { get; set; }
        public string PreviousLatitude { get; set; }
        public string PreviousLongitude { get; set; }
        public string NextLatitude { get; set; }
        public string NextLongitude { get; set; }
    }
}