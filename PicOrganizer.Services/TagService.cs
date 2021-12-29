﻿using ExifLibrary;
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
        private readonly IReportReadWriteService reportWriterService;
        ParallelOptions parallelOptions;

        public TagService(ILogger<TagService> logger, AppSettings appSettings, IReportReadWriteService reportWriterService)
        {
            this.logger = logger;
            this.appSettings = appSettings;
            this.reportWriterService = reportWriterService;

            parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = appSettings.MaxDop };
        }

        public ConcurrentBag<string> Tags { get; private set; }

        public void CreateTags(DirectoryInfo di)
        {
            logger.LogInformation("Starting to Create Tag List from pictures in Directory {Directory}", di.FullName); 
            Tags = new ConcurrentBag<string>();
            Parallel.ForEach(
                di.GetFilesViaPattern(appSettings.PictureFilter, SearchOption.AllDirectories),
                parallelOptions,
                f => AddToTagList(f, di))
                ;
        }

        private List<string> MakeWordList(FileInfo f, DirectoryInfo rootToIgnore)
        {
            var path = RemoveSpecialCharactersAndNumbers(f.FullName.Substring(rootToIgnore.FullName.Length, f.FullName.Length - rootToIgnore.FullName.Length - f.Extension.Length));
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

        public void AddRelevantTags(DirectoryInfo di)
        {
            logger.LogInformation("Starting to tag pictures in Directory {Directory}", di.FullName);
            Parallel.ForEach(
               di.GetFilesViaPattern(appSettings.PictureFilter, SearchOption.AllDirectories),
               parallelOptions,
               async f => await AddRelevantTags(f, di))
               ;
        }

        public async Task AddRelevantTags(FileInfo f, DirectoryInfo rootToIgnore)
        {
            var words = MakeWordList(f, rootToIgnore);
            var relevantTags = Tags.Intersect(words);
            string tagString = string.Join(";", relevantTags);
            ImageFile imageFile;
            try
            {
                imageFile = await ImageFile.FromFileAsync(f.FullName);
                imageFile.Properties.Set(ExifTag.WindowsKeywords, tagString);
                await imageFile.SaveAsync(f.FullName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unable to add tags {Tags} to file {File}", tagString, f.FullName);
            }
        }

        private static string RemoveSpecialCharactersAndNumbers(string str)
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