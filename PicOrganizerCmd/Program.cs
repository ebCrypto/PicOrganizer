﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PicOrganizer.Services;
using PicOrganizer.Models;
using static PicOrganizer.Services.ILocationService;
using Microsoft.Extensions.Configuration;

var config = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddEnvironmentVariables()
               .Build();
var appSettings = config.Get<AppSettings>();

Log.Logger = new LoggerConfiguration() // TODO move this to appSettings.json
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(@"c:\temp\logs\log.txt",rollingInterval: RollingInterval.Day)
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
            .AddSingleton<ITimelineToFilesService, TimelineToFilesService>()
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
    var timelineService = provider.GetRequiredService<ITimelineToFilesService>();
    var appSettings = provider.GetRequiredService<AppSettings>();
    var tagService = provider.GetRequiredService<ITagService>();
    var dirNameService = provider.GetRequiredService<IFileNameService>();
    var fileProviderService = provider.GetRequiredService<IFileProviderService>();
    var runDataService = provider.GetRequiredService<IRunDataService>();

    logger.LogInformation("Starting...");
    dirNameService.LoadCleanDirList(new FileInfo(appSettings.InputSettings.CleanDirectoryName));

    var target = new DirectoryInfo(appSettings.OutputSettings.TargetDirectory);
    if (target.Exists && appSettings.InputSettings.Mode == AppSettings.Mode.Full)
    {
        logger.LogWarning("About to delete {Target}... KILL PROGRAM IF YOU WISH TO ABORT... Or Enter to continue...", target.FullName);
        Console.ReadLine();
        logger.LogInformation(@"Deleting {Target}...", target.FullName);
        target.Delete(true);
        logger.LogInformation(@"Deleted {Target}...", target.FullName);
    }
    if (appSettings.InputSettings.Mode == AppSettings.Mode.DeltasOnly)
    {
        var metaFolder = new DirectoryInfo(Path.Combine(target.FullName,appSettings.OutputSettings.MetaDataFolderName));
        logger.LogInformation(@"Delta mode... looking for meta data in {Target}...", metaFolder.FullName);
        await runDataService.ReadFromDisk(metaFolder);
    }

    foreach ( var subFolder in appSettings.InputSettings.SourceFolders)
    {
        var source = new DirectoryInfo(subFolder);
        await copyPictureService.Copy(source, target);
    }
    await duplicateService.MoveDuplicates(target, new DirectoryInfo(Path.Combine(target.FullName , appSettings.OutputSettings.DuplicatesFolderName)));

    timelineService.LoadTimeLine(new FileInfo(appSettings.InputSettings.TimelineName));
    await locationService.WriteLocation(target, LocationWriter.FromClosestSameDay);
    await locationService.WriteLocation(target, LocationWriter.FromTimeline);

    await locationService.ReportMissing(target, "after");

    tagService.CreateTags(target);
    tagService.AddRelevantTagsToFiles(target);

    runDataService.WriteToDisk(target);
    logger.LogInformation("Done...");
}