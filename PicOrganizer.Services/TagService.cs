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

        public IEnumerable<string> Tags { get; private set; }

        public void CreateTags(DirectoryInfo di)
        {
            logger.LogInformation("Starting to Create Tag List from picture names in Directory {Directory}", di.FullName); 
            var tags = new ConcurrentBag<string>();
            Parallel.ForEach(
                fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.AllDirectories, false),
                parallelOptions,
                f => AddToTagList(f, di, tags))
                ;
            Tags = tags.Distinct();
            logger.LogInformation("Found {Count} unique Tags", Tags.Count());
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

        private void AddToTagList(FileInfo f, DirectoryInfo rootToIgnore, ConcurrentBag<string> tags)
        {
            var words = MakeWordList(f, rootToIgnore);
            Parallel.ForEach(words, parallelOptions, item => tags.Add(item.ToLowerInvariant()));
        }

        public void AddRelevantTagsToFiles(DirectoryInfo di)
        {
            logger.LogInformation("Starting to tag pictures in Directory {Directory}", di.FullName);
            Parallel.ForEach(
               fileProviderService.GetFilesViaPattern(di, appSettings.PictureFilter, SearchOption.AllDirectories, false),
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
                CompactExifLib.ExifData imageFile = new(f.FullName);
                imageFile.GetTagValue(CompactExifLib.ExifTag.XpKeywords, out string existingTags, CompactExifLib.StrCoding.Utf16Le_Byte);
                var existingTagArray = !string.IsNullOrEmpty(existingTags) && existingTags.Contains(';') ? existingTags.Split(";").ToList() : new List<string>();
                var words = MakeWordList(f, rootToIgnore);
                var relevantTags = Tags.Intersect(words).Union(existingTagArray);

                string tagString = string.Join(";", relevantTags);
                if (tagString != existingTags)
                {
                    imageFile.SetTagValue(CompactExifLib.ExifTag.XpKeywords, tagString, CompactExifLib.StrCoding.Utf16Le_Byte);
                    imageFile.Save();
                    logger.LogDebug("{File} Keywords {Old} -> {New}", f.FullName, existingTags, tagString);
                }
                logger.LogTrace("file {File} tags {Tags} were not changed", tagString, f.FullName);
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