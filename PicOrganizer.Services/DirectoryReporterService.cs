using ExifLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PicOrganizer.Services
{
    public class DirectoryReporterService : IDirectoryReporterService
    {
        private readonly ILogger<DirectoryReporterService> logger;

        public DirectoryReporterService(ILogger<DirectoryReporterService> logger)
        {
            this.logger = logger;
        }

        public async Task Report(DirectoryInfo di)
        {
            var tasks = di.GetFiles().Select(f => LogInfo(f)).ToList();
            await Task.WhenAll(tasks);
        }

        private async Task LogInfo(FileInfo fileInfo)
        {
            try
            {
                ImageFile imageFile;
                try
                {
                    imageFile = ImageFile.FromFile(fileInfo.FullName);
                    ExifProperty? tag;
                    tag = imageFile.Properties.Get(ExifTag.DateTimeOriginal);
                }
                catch (NotValidImageFileException)
                {
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ExceptionMessage} {FileName}", ex.Message, fileInfo.Name);
            }
        }
    }
}
