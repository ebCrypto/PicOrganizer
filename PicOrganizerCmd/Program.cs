using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PicOrganizer.Services;
using PicOrganizer.Models;

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

    var root = new DirectoryInfo(@"C:\temp\");
    var target = new DirectoryInfo(@"C:\temp\Emmanuel2");    

    var source_1 = new DirectoryInfo(@"C:\temp\Flickr33");
    var source_2 = new DirectoryInfo(@"C:\temp\google-photos");
    var source_3 = new DirectoryInfo(@"C:\temp\RebelXti");
    var source_4 = new DirectoryInfo(@"C:\temp\samsung-lg");

    if (target.Exists)
    {
        target.Delete(true);
        logger.LogInformation(@"Deleted {Target}...", target.FullName);
    }
    await copyPictureService.Copy(source_1, target);
    await copyPictureService.Copy(source_4, target);
    await copyPictureService.Copy(source_3, target);
    await copyPictureService.Copy(source_2, target);

    await duplicateService.MoveDuplicates(target, new DirectoryInfo(target.FullName + "-" + appSettings.DuplicatesFolderName));

    await locationService.ReportMissing(target);
    timelineService.LoadTimeLine(new FileInfo(@"C:\temp\eb-timeline.csv"));
    await timelineService.AddlocationFromTimeLine(target);
    await locationService.WriteLocationFromClosestKnownIfSameDay(target);
    await locationService.ReportMissing(target);

    tagService.CreateTags(target);

    logger.LogInformation("Done...");
}