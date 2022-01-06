namespace PicOrganizer.Models
{
    public class MetaDataRun
    {
        public Guid Id { get; set; }
        public DateTimeOffset startTime { get; set; }
        public DateTimeOffset endTime { get; set; }
        public List<MetaDataFolder> Folders { get; set; }           
    }
}
