# PicOrganizer
PicOrganizer is an dotnet core library for organizing, dating, geotagging and keyword tagging a collection of pictures and videos using file names, directory names and information from files taken at about the same time with the purpose of creating a consistent and organized photo library on the file system.


## Typical use case
You inherit a large collection of pictures taken by camera that was not equipped with a GPS device, or a libray of pictures that was converted from prints or slides. These pictures might be organized in folders with dates and/or locations contained within the directory names or filenames. 

PicOrganizer will extract the dates from the file names and will buble up to the directory names until a date is recognized. PicOrganizer will copy these files using a structured hierarchy of folders `yyyy-MM`, and then add the GPS coordinates to the files using either or a combination of 3 methods: From the FileName, a known timeline (ie where was I on this date) or using the location of the closest found picture on that same day.

## Features Overview

## Getting Started

## Upcoming Features and Improvements

