using PicOrganizer.Models;

namespace PicOrganizer.Services
{
    public interface IRunDataService
    {
        void WriteToDisk(DirectoryInfo target);
        void Add(IEnumerable<FileInfo> result, DirectoryInfo di, IFileProviderService.FileType fileType);
        Task ReadFromDisk(DirectoryInfo source);
        IEnumerable<string> ExceptionList();
    }
}