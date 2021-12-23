namespace PicOrganizer.Services
{
    public interface IDuplicatesService
    {
        Task MoveDuplicates(DirectoryInfo di, DirectoryInfo destination);
    }
}