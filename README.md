# PicOrganizer
PicOrganizer is an dotnet core library for organizing, dating, geotagging and keyword tagging a collection of pictures and videos using file names, directory names and information from files taken at about the same time with the purpose of creating a consistent and organized photo library on the file system.


## Typical use case
You inherit a large collection of pictures taken by camera that was not equipped with a GPS device, or a libray of pictures that was converted from prints or slides. These pictures might be organized in folders with dates and/or locations contained within the directory names or filenames. 

PicOrganizer will extract the dates from the file names and will buble up to the directory names until a date is recognized. PicOrganizer will copy these files using a structured hierarchy of folders `yyyy-MM`, and then add the GPS coordinates to the files using either or a combination of 3 methods: From the FileName, a known timeline (ie where was I on this date) or using the location of the closest found picture on that same day.

## Features Overview
* Service Based architecture to facilitate calling all or a subset of the processing routines.
* Will not alter the original files.
* `Full Mode`: will delete the target directory and rebuild from input data.
* `Delta Mode`: will use the meta data from the most recent execution and only add the new files to the library.
* Combine multiple photos and videos directories into on target directory, using a structured hierarchy `yyyy-MM` or other format.
* Loads csv list of known typos to correct file and directory names.
* Loads csv list of known location (ie Disneyland refers to 33.810561, -117.919494).
* Loads csv list of know locations within a time range (ie Files without geo tags between 1/2/13 14:35 and 2/3/13 17:58 are taken in New York City).
* Move duplicates files to a separate destination.
* Explicitly and individually specify files that should not be included in the target directory.
* Infer dates from file names (ie `Christmas 2015` or `Jan 12th, 2013` or `whatsapp` format will be converted to the proper DateTime).
* Generate csv reports to quickly identify what files are missing dates or locations.
* Add geo-tagging data using 3 methods:
  - From file name.
  - From Closest known location on same day.
  - From timeline. 
* Create list of keywords from filename, load a list of exclusion (ie common words) and tag files.
* Remove special characters from filename as they might be misunterpreted by the libray client.
* Save meta data (list of processed files) to json for easy review.

### Main dependencies
* `Serilog` for logging with multiple verbosity levels.
* `CsvHelper` to manage csv files.
* `CompactExifLib` to read and write pictures Exif Data.
* `Microsoft.Recognizers.Text.DateTime` to extract dates from file name in different langages.

## Getting Started
_Tested on Windows 10, 11 and Ubuntu 20.04._

Update the appSettings.json file with your parameters such as input directories and destination:
```
"InputSettings": {
    "SourceFolders": [
      "C:\\photolibary-source\\flickr",
      "C:\\photolibary-source\\google-photos",
      "C:\\photolibary-source\\cannon"
    ],
    "Culture": "en-us",
    "CleanDirectoryName": "C:\\photolibary-source\\clean-directory-names.csv",
    "TimelineName": "C:\\photolibary-source\\my-timeline.csv",
    "KnownLocations": "C:\\photolibary-source\\known-locations.csv",
    "StartingYearOfLibrary": 1970,
    "ExcludedFiles": [
      "IMG_0001.JPG",
      "IMG_0010.JPEG",
      "IMG_0123.JPEG"
    ],
    "Scanned": "scanned",
    "Mode": 0, // 0 is Full, 1 is Delta only
    "RetryAttempts": 2
  },
  "OutputSettings": {
    "TargetDirectories": [ "C:\\photo-library\\pic-organizer" ],
```

Make sure to grab the csv samples from the [csv-samples](csv-samples) folder:

### my-timeline.csv
```
Start,End,Latitude,Longitude,MissingAddress,SampleFileName
02/27/2016 00:00:00,04/27/2018 23:59:59,37.616600,-122.384895,"SFO",""
```
### known-locations.csv
```
NameInFile,Latitude,Longitude,ActualLocation
Paris,48.853001,2.349800,Notre Dame de Paris
```
### clean-directory-names.csv
```
Original,ReplaceWith
19810,1981
8-88,08-1988
 1-81,01-1981
Newyork,New-York 
```
Execute the entire `DoWork` method or comment out some services, potentially to execute for a subsequent run.
```
    locationService.ReportMissing(target, LocationWriter.Before);
    if (!string.IsNullOrEmpty(appSettings.InputSettings.KnownLocations) && knownLocationsFile.Exists)
        await locationService.WriteLocation(target, LocationWriter.FromFileName);
    await locationService.WriteLocation(target, LocationWriter.FromClosestSameDay);
    if (!string.IsNullOrEmpty(appSettings.InputSettings.TimelineName) && timelineFile.Exists)
        await locationService.WriteLocation(target, LocationWriter.FromTimeline);
    locationService.ReportMissing(target, LocationWriter.After);
```

## Upcoming Features and Improvements
Pull Requests welcome!
* [ ] Create a cli using https://github.com/dotnet/command-line-api
* [ ] Integrate geo-tagging API (Google maps or similar) to extract location from filename instead of using csv list
* [ ] Dockerize



