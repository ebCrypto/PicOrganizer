using Xunit;
using PicOrganizer.Services;
using PicOrganizer.Models;
using System.Threading.Tasks;

namespace PicOrganizer.Tests;

public class TestDateRecognizerService
{
    private AppSettings appSettings { get; set; }
    private VerifiableLogger<DateRecognizerService> verifiableLogger { get; set; }

    public TestDateRecognizerService()
    {
        appSettings = new AppSettings();
        verifiableLogger = new VerifiableLogger<DateRecognizerService>();
    }

    [Fact]
    public async Task Test1()
    {
        var s = new DateRecognizerService(verifiableLogger, appSettings);
        var inferredName = await s.InferDateFromName("IMG_20211219.jpg");
        Assert.Equal(new System.DateTime(2021, 12, 19), inferredName);
    }

    [Theory]
    [InlineData("2018-08-18.png", 2018, 08, 18, 00, 00, 00)]
    [InlineData("20180818.png", 2018, 08, 18, 00, 00, 00)]
    [InlineData("2017-08-18-08-25-49.png", 2017, 08, 18, 08, 25, 49)]
    [InlineData("Screenshot_2016-03-24-14-14-03.jpg", 2016,3,24,14,14,03)]
    [InlineData("Capture+_2017-07-10-06-26-22.jpg", 2017,07,10,06,26,22)]
    [InlineData("Photo on 10-22-17 at 6.29 PM #2.jpg", 2017,10,22,18,29,00)]
    [InlineData("20130831_150637-edited.jpg", 2013,08,31,15,06,37)]
    public async Task TestRecognizer(string name, int yyyy, int MM, int dd, int HH, int min, int ss)
    {
        var s = new DateRecognizerService(verifiableLogger, appSettings);
        var inferredName = await s.InferDateFromName(name);
        Assert.Equal(new System.DateTime(yyyy, MM, dd, HH, min, ss), inferredName);
    }
}