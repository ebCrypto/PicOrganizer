namespace PicOrganizer.Models
{
    public class MetaDataFolder
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public Dictionary<string,MetaDataFile> Files { get; set; }
    }
}
