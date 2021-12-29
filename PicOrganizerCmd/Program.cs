using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PicOrganizer.Services;
using PicOrganizer.Models;
using static PicOrganizer.Services.ILocationService;

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
            .AddSingleton<AppSettings>()
            .AddSingleton<ICopyDigitalMediaService, CopyDigitalMediaService>()
            .AddSingleton<IDirectoryNameService, DirectoryNameService>()
            .AddSingleton<ILocationService, LocationService>()
            .AddSingleton<IDuplicatesService, DuplicatesService>()
            .AddSingleton<IReportReadWriteService, CsvReadWriteService>()
            .AddSingleton<IFileNameCleanerService, FileNameCleanerService>()
            .AddSingleton<ITimelineToFilesService, TimelineToFilesService>()
            .AddSingleton<IDateRecognizerService, DateRecognizerService>()
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

    logger.LogInformation("Starting...");

    var root = new DirectoryInfo(@"C:\temp\source");
    var target = new DirectoryInfo(@"C:\temp\Emmanuel");    

    var source_1 = new DirectoryInfo(Path.Combine(root.FullName,"Flickr"));
    var source_2 = new DirectoryInfo(Path.Combine(root.FullName, "google-photos"));
    var source_3 = new DirectoryInfo(Path.Combine(root.FullName, "RebelXti"));
    var source_4 = new DirectoryInfo(Path.Combine(root.FullName, "samsung-lg"));
    var source_5 = new DirectoryInfo(Path.Combine(root.FullName, "iPhone"));

    if (target.Exists)
    {
        target.Delete(true);
        logger.LogInformation(@"Deleted {Target}...", target.FullName);
    }
    await copyPictureService.Copy(source_5, target);
    await copyPictureService.Copy(source_1, target);
    await copyPictureService.Copy(source_4, target);
    await copyPictureService.Copy(source_3, target);
    await copyPictureService.Copy(source_2, target);

    await duplicateService.MoveDuplicates(target, new DirectoryInfo(target.FullName + "-" + appSettings.DuplicatesFolderName));

    await locationService.ReportMissing(target, "before");

    timelineService.LoadTimeLine(new FileInfo(@"C:\temp\eb-timeline.csv"));
    await locationService.WriteLocation(target, LocationWriter.FromClosestSameDay);
    await locationService.WriteLocation(target, LocationWriter.FromTimeline);

    await locationService.ReportMissing(target, "after");

    tagService.CreateTags(target);
    tagService.AddRelevantTags(target);

    logger.LogInformation("Done...");
}