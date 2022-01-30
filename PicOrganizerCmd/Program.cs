using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using PicOrganizer.Services;
using PicOrganizer.Models;
using static PicOrganizer.Services.ILocationService;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings-bernard-papier.json")
               .AddEnvironmentVariables()
               .Build();
var appSettings = config.Get<AppSettings>();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(config)
    .CreateLogger();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
        services
            .AddSingleton<AppSettings>(appSettings)
            .AddSingleton<ICopyDigitalMediaService, CopyDigitalMediaService>()
            .AddSingleton<ILocationService, LocationService>()
            .AddSingleton<IDuplicatesService, DuplicatesService>()
            .AddSingleton<IReportReadWriteService, CsvReadWriteService>()
            .AddSingleton<IFileNameService, FileNameService>()
            .AddSingleton<IDateRecognizerService, DateRecognizerService>()
            .AddSingleton<IFileProviderService, FileProviderService>()
            .AddSingleton<IRunDataService, RunDataService>()
            .AddSingleton<ITagService, TagService>()
            )
    .UseSerilog()
    .Build();

DoWork(host.Services);

await host.RunAsync();

static async void DoWork(IServiceProvider services)
{
    using var serviceScope = services.CreateScope();
    var provider = serviceScope.ServiceProvider;
    var copyPictureService = provider.GetRequiredService<ICopyDigitalMediaService>();
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var locationService = provider.GetRequiredService<ILocationService>();
    var duplicateService = provider.GetRequiredService<IDuplicatesService>();
    var appSettings = provider.GetRequiredService<AppSettings>();
    var tagService = provider.GetRequiredService<ITagService>();
    var dirNameService = provider.GetRequiredService<IFileNameService>();
    var fileProviderService = provider.GetRequiredService<IFileProviderService>();
    var runDataService = provider.GetRequiredService<IRunDataService>();
    FileInfo timelineFile = null;
    FileInfo knownLocationsFile = null;

    logger.LogInformation("Starting...");

    var target = new DirectoryInfo(appSettings.OutputSettings.TargetDirectories.FirstOrDefault());
    if (target.Exists && appSettings.InputSettings.Mode == AppSettings.Mode.Full)
    {
        logger.LogWarning("About to delete {Target}... KILL PROGRAM IF YOU WISH TO ABORT... Or Enter to continue...", target.FullName);
        Console.ReadLine();
        logger.LogInformation(@"Deleting {Target}...", target.FullName);
        target.Delete(true);
        logger.LogInformation(@"Deleted {Target}...", target.FullName);
    }
    Directory.CreateDirectory(Path.Combine(target.FullName, appSettings.OutputSettings.InputBackupFolderName));

    if (!string.IsNullOrEmpty(appSettings.InputSettings.CleanDirectoryName))
    {
        var cleanDirList = new FileInfo(appSettings.InputSettings.CleanDirectoryName);
        if (cleanDirList.Exists)
        {
            dirNameService.LoadCleanDirList(cleanDirList);
            cleanDirList.CopyTo(Path.Combine(target.FullName, appSettings.OutputSettings.InputBackupFolderName, cleanDirList.Name));
        }
        else
            logger.LogError("cleanDirList file {File} does not exist", cleanDirList.FullName);
    }
    if (!string.IsNullOrEmpty(appSettings.InputSettings.TimelineName))
    {
        timelineFile = new FileInfo(appSettings.InputSettings.TimelineName);
        if (timelineFile.Exists)
        {
            locationService.LoadTimeLine(timelineFile);
            timelineFile.CopyTo(Path.Combine(target.FullName, appSettings.OutputSettings.InputBackupFolderName, timelineFile.Name));
        }
        else
            logger.LogError("TimeLine file {File} does not exist", timelineFile.FullName);
    }
    if (!string.IsNullOrEmpty(appSettings.InputSettings.KnownLocations))
    {
        knownLocationsFile = new FileInfo(appSettings.InputSettings.KnownLocations);
        if (knownLocationsFile.Exists)
        {
            locationService.LoadKnownLocations(knownLocationsFile);
            knownLocationsFile.CopyTo(Path.Combine(target.FullName, appSettings.OutputSettings.InputBackupFolderName, knownLocationsFile.Name));
        }
        else
            logger.LogError("KnownLocationsFile file {File} does not exist", knownLocationsFile.FullName);
    }

    if (appSettings.InputSettings.Mode == AppSettings.Mode.DeltasOnly)
    {
        var metaFolder = new DirectoryInfo(Path.Combine(target.FullName, appSettings.OutputSettings.MetaDataFolderName));
        if (!metaFolder.Exists)
        {
            logger.LogError("Unable to find Meta Data folder {Meta}", metaFolder.FullName);
            Environment.Exit(-1);
        }
        logger.LogInformation(@"Delta mode... looking for meta data in {Target}...", metaFolder.FullName);
        await runDataService.ReadFromDisk(metaFolder);
    }

    await copyPictureService.Copy(target);
    await duplicateService.MoveDuplicates(target, new DirectoryInfo(Path.Combine(target.FullName, appSettings.OutputSettings.DuplicatesFolderName)));

    locationService.ReportMissing(target, "before");
    if (!string.IsNullOrEmpty(appSettings.InputSettings.KnownLocations) && knownLocationsFile.Exists)
        await locationService.WriteLocation(target, LocationWriter.FromFileName);
    await locationService.WriteLocation(target, LocationWriter.FromClosestSameDay);
    if (!string.IsNullOrEmpty(appSettings.InputSettings.TimelineName) && timelineFile.Exists)
        await locationService.WriteLocation(target, LocationWriter.FromTimeline);
    locationService.ReportMissing(target, "after");

    tagService.CreateTags(target);
    tagService.AddRelevantTagsToFiles(target);

    runDataService.WriteToDisk(target);

    logger.LogInformation("Done...");
}