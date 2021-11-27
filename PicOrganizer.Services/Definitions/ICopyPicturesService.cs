namespace PicOrganizer.Services
{
    public interface ICopyPicturesService
    {
        Task Copy(DirectoryInfo from, DirectoryInfo to);
    }
}