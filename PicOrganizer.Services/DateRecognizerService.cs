﻿using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using PicOrganizer.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PicOrganizer.Services
{
    public class DateRecognizerService : IDateRecognizerService
    {
        private readonly ILogger<DateRecognizerService> _logger;
        private readonly AppSettings appSettings;

        public DateRecognizerService(ILogger<DateRecognizerService> logger, AppSettings appSettings)
        {
            this._logger = logger;
            this.appSettings = appSettings;
        }

        public DateTime InferDateFromName(string name)
        {
            foreach (var dateFormat in appSettings.KnownUsedNameFormats)
            {
                string noAlphaChar = Regex.Replace(name, "[^0-9_-]", "");
                DateTime.TryParseExact(noAlphaChar, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                if (result != DateTime.MinValue)
                    return result;
            }

            List<ModelResult>? modelResults = DateTimeRecognizer.RecognizeDateTime(name, Culture.English);
            if (modelResults.Any())
            {
                foreach (var modelResult in modelResults)
                {
                    SortedDictionary<string, object>? resolution = modelResult.Resolution;
                    _logger.LogDebug("Found {Count} item(s) while attempting date resolution for name {Name}", resolution.Count(), name);
                    foreach (KeyValuePair<string, object> resolutionValue in resolution)
                    {
                        var value = (List<Dictionary<string, string>>)resolutionValue.Value;
                        _logger.LogTrace("Found {Count} value(s) in this resolution for name {Name}", value.Count, name);
                        DateTime.TryParse(value?[0]?["timex"], out var result);
                        if (result.Year > appSettings.StartingYearOfLibrary && result < DateTime.Now)
                        {
                            _logger.LogInformation("Inferring DateTaken '{Date}' from name {Name}", result.ToString(), name);
                            return result;
                        }
                    }
                }
            }
            return DateTime.MinValue;
        }
    }
}
