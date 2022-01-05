using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PicOrganizer.Services;
using PicOrganizer.Models;
using static PicOrganizer.Services.ILocationService;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

var config = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddEnvironmentVariables()
               .Build();
var appSettings = config.Get<AppSettings>();

Log.Logger = new LoggerConfiguration()
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

    logger.LogInformation("Starting...");
    dirNameService.LoadCleanDirList(new FileInfo(appSettings.InputSettings.CleanDirectoryName));

    var target = new DirectoryInfo(appSettings.OutputSettings.TargetDirectory);
    if (target.Exists && appSettings.InputSettings.Mode == AppSettings.Mode.AllAndErase)
    {
        target.Delete(true);
        logger.LogInformation(@"Deleted {Target}...", target.FullName);
    }
    else if (appSettings.InputSettings.Mode == AppSettings.Mode.FindDeltasAndAdd)
    {
        logger.LogCritical("{Target} does not exist and {Mode} is set to update", target.FullName, appSettings.InputSettings.Mode);
        return;
    }
    if (appSettings.InputSettings.Mode == AppSettings.Mode.FindDeltasAndAdd)
    {
        fileProviderService.SetExcept(null); //TODO complete this
    }

    foreach ( var subFolder in appSettings.InputSettings.Subfolders)
    {
        var source = new DirectoryInfo(Path.Combine(appSettings.InputSettings.RootDirectory, subFolder));
        var files = await copyPictureService.Copy(source, target);
        var f = JsonSerializer.Serialize(files.Select(p=>new { p.FullName, p.Name, p.Length, p.LastWriteTimeUtc, p.Extension }));
        var directory = new DirectoryInfo(target.FullName + "-" + "metaData");
        Directory.CreateDirectory(directory.FullName);
        File.WriteAllText(Path.Combine(directory.FullName, subFolder + ".json"), f );
    }
    await duplicateService.MoveDuplicates(target, new DirectoryInfo(target.FullName + "-" + appSettings.OutputSettings.DuplicatesFolderName));
    //await locationService.ReportMissing(target, "before");

    timelineService.LoadTimeLine(new FileInfo(appSettings.InputSettings.TimelineName));
    await locationService.WriteLocation(target, LocationWriter.FromClosestSameDay);
    await locationService.WriteLocation(target, LocationWriter.FromTimeline);

    //await locationService.ReportMissing(target, "after");

    tagService.CreateTags(target);
    tagService.AddRelevantTagsToFiles(target);

    logger.LogInformation("Done...");
}