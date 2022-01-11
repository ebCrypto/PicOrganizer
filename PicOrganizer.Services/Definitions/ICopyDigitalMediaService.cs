namespace PicOrganizer.Services
{
    public interface ICopyDigitalMediaService
    {
        Task Copy(DirectoryInfo to);
        void PropagateToOtherTargets();
    }
}