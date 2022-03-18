using Xunit;
using PicOrganizer.Services;
using PicOrganizer.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace PicOrganizer.Tests;

public class TestDateRecognizerService
{
    private AppSettings appSettings { get; set; }
    private VerifiableLogger<DateRecognizerService> verifiableLogger { get; set; }

    public TestDateRecognizerService()
    {
        var config = new ConfigurationBuilder()
                  //.AddJsonFile(Path.Combine(v @"..\..\..\..\PicOrganizerCmd\appSettings.json"))
                  .AddJsonFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PicOrganizerCmd", "appSettings.json"))
                  .Build();
        appSettings = config.Get<AppSettings>();

        verifiableLogger = new VerifiableLogger<DateRecognizerService>();
    }

    [Theory]
    [InlineData("IMG_20211219.jpg", 2021,12,19, 00, 00)]
    [InlineData("2018-08-18.png", 2018, 08, 18, 00, 00)]
    [InlineData("20180818.png", 2018, 08, 18, 00, 00)]
    [InlineData("2017-08-18-08-25-49.png", 2017, 08, 18, 08, 25, 49)]
    [InlineData("Screenshot_2016-03-24-14-14-03.jpg", 2016,3,24,14,14,03)]
    [InlineData("Capture+_2017-07-10-06-26-22.jpg", 2017,07,10,06,26,22)]
    [InlineData("Photo on 10-22-17 at 6.29 PM #2.jpg", 2017,10,22,18,29)]
    [InlineData("20130831_150637-edited.jpg", 2013,08,31,15,06,37)]
    [InlineData("IMG-20170412-WA0000.jpg", 2017,04,12,0,0)]
    [InlineData("IMG_20161104_120129_01.jpg",2016,11,04,12,01,29,10)]
    [InlineData("VZM.IMG_20170125_063112.jpg",2017,01,25,06,31,12)]
    [InlineData("20160515_181724_26984653931.mp4", 2016,05,15,18,17,24)]
    [InlineData("0803171824_36389065576.mp4", 2017,08,03,18,24)]
    [InlineData("0603170741.mp4",2017,06,03,07,41)]
    [InlineData("Photo on 10-23-17 at 9.59 AM.jpeg",2017,10,23,09,59)]
    public void TestRecognizer(string name, int yyyy, int MM, int dd, int HH, int min, int ss=0, int fff=0)
    {
        var s = new DateRecognizerService(verifiableLogger, appSettings);
        var inferredName = s.InferDateFromName(name);
        Assert.Equal(new System.DateTime(yyyy, MM, dd, HH, min, ss, fff), inferredName);
    }
}