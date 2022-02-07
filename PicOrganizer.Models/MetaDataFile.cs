namespace PicOrganizer.Models
{
    public class MetaDataFile
    {
        public MetaDataFile(FileInfo p)
        {
            if (p!= null && p.Exists)
            {
                Name = p.Name;
                FullName = p.FullName;  
                Length = p.Length;
                LastWriteTimeUtc = p.LastWriteTimeUtc;
                Extension = p.Extension;
            }
        }

        // for desiarlization purposes
        public MetaDataFile()
        {
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public string Extension { get; set; }
    }
}
