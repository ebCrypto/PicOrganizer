namespace PicOrganizer.Models
{
    public class MetaDataFile
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string Extension { get; set; }
    }
}
