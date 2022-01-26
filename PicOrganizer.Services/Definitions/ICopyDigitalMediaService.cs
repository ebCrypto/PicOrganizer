namespace PicOrganizer.Services
{
    public interface ICopyDigitalMediaService
    {
        Task Copy(DirectoryInfo to);
        
        [Obsolete]
        void PropagateToOtherTargets();
    }
}