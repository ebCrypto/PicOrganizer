namespace PicOrganizer.Models
{
    public class ReportDetail
    {
        public string FileName { get; set; }
        public DateTime DateTime { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string Address { get; set; }
        public bool Update { get; set; }

    }
}