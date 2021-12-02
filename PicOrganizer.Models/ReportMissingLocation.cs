namespace PicOrganizer.Models
{
    public class ReportMissingLocation
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string SampleFileName { get; set; }
        public string MissingAddress { get; set; }
    }
}