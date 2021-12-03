using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using PicOrganizer.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((_, services) =>
        services
            .AddSingleton<ICopyPicturesService, CopyPicturesService>()
            .AddSingleton<IDirectoryNameService, DirectoryNameService>()
            .AddSingleton<DirectoryLocationReporterService>()
            .AddSingleton<DirectoryDuplicateReporterService>()
            .AddSingleton<IReportWriterService, CsvReportWriterService>()
            )
    .UseSerilog()
    .Build();

DoWork(host.Services);

await host.RunAsync();

static async void DoWork(IServiceProvider services)
{
    using var serviceScope = services.CreateScope();
    var provider = serviceScope.ServiceProvider;
    var copyPictureService = provider.GetRequiredService<ICopyPicturesService>();
    var logger = provider.GetRequiredService<ILogger<Program>>();
    var locationReporter = provider.GetRequiredService<DirectoryLocationReporterService>();
    var duplicateReporter = provider.GetRequiredService<DirectoryDuplicateReporterService>();

    logger.LogInformation("Starting...");
    var source_1 = new DirectoryInfo(@"C:\temp\Flickr28");
    var source_2 = new DirectoryInfo(@"C:\temp\google-photos");
    var source_3 = new DirectoryInfo(@"C:\temp\RebelXti");
    var target = new DirectoryInfo(@"C:\temp\AllPics3");

    //if (target.Exists)
    //{
    //    target.Delete(true);
    //    logger.LogInformation(@"Deleted {Target}...", target.FullName);
    //}
    //await copyPictureService.Copy(source_3, target);
    //await copyPictureService.Copy(source_2, target);
    //await copyPictureService.Copy(source_1, target);

    await duplicateReporter.Report(target);
    await locationReporter.Report(target);

    logger.LogInformation("Done...");
}