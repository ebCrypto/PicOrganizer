using Xunit;
using PicOrganizer.Services;
using PicOrganizer.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using Moq;

namespace PicOrganizer.Tests;

public class TestLocationService
{
    private AppSettings appSettings { get; set; }
    private VerifiableLogger<LocationService> verifiableLogger { get; set; }

    public TestLocationService ()
    {
        var config = new ConfigurationBuilder()
                  .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PicOrganizerCmd", "appSettings.json"))
                  .Build();
        appSettings = config.Get<AppSettings>();

        verifiableLogger = new VerifiableLogger<LocationService>();
    }


    [Fact]
    public void TestSaveCoordinatesToImage()
    {
        var reportService = new Mock<IReportReadWriteService>();
        var fileProviderService = new Mock<IFileProviderService>();
        var dateRecognizerService = new Mock<IDateRecognizerService>();
        var l = new LocationService(appSettings, verifiableLogger, reportService.Object, fileProviderService.Object, dateRecognizerService.Object);
        var tempFile = new FileInfo(Path.GetTempFileName());
        l.SaveCoordinatesToImage(1.23m, 4.56m, tempFile);
        Assert.Equal(1,verifiableLogger.ExceptionCalledCount);
    }
    
}
