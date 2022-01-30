using ExifLibrary;
using Microsoft.Extensions.Logging;
using PicOrganizer.Models;
using System.Collections.Concurrent;
using System.Text;

namespace PicOrganizer.Services
{
    public class TagService : ITagService
    {
        private readonly ILogger<TagService> logger;
        private readonly AppSettings appSettings;
        private readonly IFileProviderService fileProviderService;
        ParallelOptions parallelOptions;

        public TagService(ILogger<TagService> logger, AppSettings appSettings, IFileProviderService fileProviderService)
        {
            this.logger = logger;
            this.appSettings = appSettings;
            this.fileProviderService = fileProviderService;
            parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = appSettings.MaxDop };
        }

        public ConcurrentBag<string> Tags { get; private set; }

        public void CreateTags(DirectoryInfo di)
        {
            logger.LogInformation("Starting to Create Tag List from pictures in Directory {Directory}", di.FullName); 
            Tags = new ConcurrentBag<string>();
            Parallel.ForEach(
                fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.AllDirectories),
                parallelOptions,
                f => AddToTagList(f, di))
                ;
        }

        private List<string> MakeWordList(FileInfo f, DirectoryInfo rootToIgnore)
        {
            var path = AlphaCharAndSpacesOnly(f.FullName.Substring(rootToIgnore.FullName.Length, f.FullName.Length - rootToIgnore.FullName.Length - f.Extension.Length));
            if (path.Contains(" "))
            {
                var items = path.Split(" ").Select(p => p.ToLowerInvariant()).Where(p => !string.IsNullOrWhiteSpace(p) && p.Length > 2 && !appSettings.TagSkipper.Contains(p));
                return items.ToList();
            }
            return new List<string>();
        }

        private void AddToTagList(FileInfo f, DirectoryInfo rootToIgnore)
        {
            var words = MakeWordList(f, rootToIgnore);
            Parallel.ForEach(words, parallelOptions, item => Tags.Add(item.ToLowerInvariant()));
        }

        public void AddRelevantTagsToFiles(DirectoryInfo di)
        {
            logger.LogInformation("Starting to tag pictures in Directory {Directory}", di.FullName);
            Parallel.ForEach(
               fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.AllDirectories),
               parallelOptions,
               async f => await AddRelevantTagsToFile(f, di))
               ;
            logger.LogInformation("Done tagging pictures in Directory {Directory}", di.FullName);
        }

        public async Task AddRelevantTagsToFile(FileInfo f, DirectoryInfo rootToIgnore)
        {
            try
            {
                if (f.Directory.Name == appSettings.OutputSettings.InvalidJpegFolderName)
                {
                    logger.LogTrace("Skiping invalid file {File}", f.FullName);
                    return;
                }
                ImageFile imageFile;
                imageFile = await ImageFile.FromFileAsync(f.FullName);
                var existingTags = (string)imageFile.Properties.Get(ExifTag.WindowsKeywords)?.Value;
                var existingTagArray = !string.IsNullOrEmpty(existingTags) && existingTags.Contains(';') ? existingTags.Split(";").ToList() : new List<string>() ;
                var words = MakeWordList(f, rootToIgnore);
                var relevantTags = Tags.Intersect(words).Intersect(existingTagArray);
                
                string tagString = string.Join(";", relevantTags);
                imageFile.Properties.Set(ExifTag.WindowsKeywords, tagString);
                await imageFile.SaveAsync(f.FullName);
                logger.LogTrace("Added tags {Tags} to file {File}",tagString, f.FullName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Unable to add tags to file {File}", f.FullName);
            }
        }

        private static string AlphaCharAndSpacesOnly(string str)
        {
            StringBuilder sb = new();
            foreach (char c in str)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                    sb.Append(c);
                else
                    sb.Append(" ");
            }
            return sb.ToString();
        }
    }
}