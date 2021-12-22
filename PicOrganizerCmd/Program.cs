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
    .WriteTo.File(@"c:\temp\log.txt",rollingInterval: RollingInterval.Day)
    .CreateLogger();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
        services
            .AddSingleton<ICopyDigitalMediaService, CopyDigitalMediaService>()
            .AddSingleton<IDirectoryNameService, DirectoryNameService>()
            .AddSingleton<LocationService>()
            .AddSingleton<DirectoryDuplicateReporterService>()
            .AddSingleton<AppSettings>()
            .AddSingleton<IReportWriterService, CsvReportWriterService>()
            .AddSingleton<IFileNameCleanerService, FileNameCleanerService>()
            .AddSingleton<ITimelineToFilesService, TimelineToFilesService>()
            .AddSingleton<IDateRecognizerService, DateRecognizerService>()
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
    var locationReporter = provider.GetRequiredService<LocationService>();
    var duplicateReporter = provider.GetRequiredService<DirectoryDuplicateReporterService>();
    var timelineService = provider.GetRequiredService<ITimelineToFilesService>();
    var appSettings = provider.GetRequiredService<AppSettings>();

    logger.LogInformation("Starting...");

    var target = new DirectoryInfo(@"C:\\temp\AllPics62");    

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

    await duplicateReporter.ReportAndMoveDuplicates(target, new DirectoryInfo(target.FullName + "-" + appSettings.DuplicatesFolderName));
    await locationReporter.Report(target);

    timelineService.LoadTimeLine(new FileInfo(@"C:\temp\eb-timeline.csv"));
   //await timelineService.AddlocationFromTimeLine(new DirectoryInfo(@"C:\temp\AllPics13\2003-12")); ;


    logger.LogInformation("Done...");
}