namespace PicOrganizer.Services
{
    public interface ICopyDigitalMediaService
    {
        Task<IEnumerable<FileInfo>> Copy(DirectoryInfo from, DirectoryInfo to);
    }
}