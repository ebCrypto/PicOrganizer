﻿using Microsoft.Extensions.DependencyInjection;
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
            )
    .UseSerilog()
    .Build();

ProcessFolders(host.Services); 

await host.RunAsync();

static async void ProcessFolders(IServiceProvider services)
{
    using var serviceScope = services.CreateScope();
    var provider = serviceScope.ServiceProvider;
    var copyPictureService = provider.GetRequiredService<ICopyPicturesService>();
    var logger = provider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Starting...");
    var target = new DirectoryInfo(@"C:\temp\Flickr01");
    target.Delete(true);
    logger.LogInformation(@"Deleted {Target}...", target.FullName);

    logger.LogInformation(@"Processing C:\temp\Flickr24\Auto Upload...");
    await copyPictureService.Copy(new DirectoryInfo(@"C:\temp\Flickr24\Auto Upload"), target);

    logger.LogInformation(@"Processing C:\temp\Flickr24\No Album...");
    await copyPictureService.Copy(new DirectoryInfo(@"C:\temp\Flickr24\No Album"), target);
    logger.LogInformation("Done...");
}