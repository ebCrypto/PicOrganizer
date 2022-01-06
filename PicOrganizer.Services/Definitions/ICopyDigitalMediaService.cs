namespace PicOrganizer.Services
{
    public interface ICopyDigitalMediaService
    {
        Task Copy(DirectoryInfo from, DirectoryInfo to);
    }
}