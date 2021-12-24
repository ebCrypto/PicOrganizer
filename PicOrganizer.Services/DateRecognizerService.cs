using Microsoft.Extensions.Logging;
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

        private DateTime InferDateFromWhatsappName(string name)
        {
            var regex = new Regex(@"IMG-[0-9]{8}-WA[0-9]{4}");
            if (regex.IsMatch(name))
            {
                DateTime.TryParseExact(name.Substring(4,8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                return result;
            }
            else
                return DateTime.MinValue;
        }

        public DateTime InferDateFromName(string name)
        {
            if (name.Contains("."))
                name = name.Substring(0,name.LastIndexOf("."));
            var whatsapp = InferDateFromWhatsappName(name);
            if (whatsapp != DateTime.MinValue)
                return whatsapp;
            string noAlphaChar = Regex.Replace(name, "[^0-9_-]", "");            
            foreach (var dateFormat in appSettings.KnownUsedNameFormats)
            {

                DateTime.TryParseExact(noAlphaChar, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                if (result != DateTime.MinValue)
                    return result;
            }
            noAlphaChar = noAlphaChar.Replace("-", "_");            
            foreach (var dateFormat in appSettings.KnownUsedNameFormats)
            {
                if (noAlphaChar.Contains("_"))
                {
                    var items = noAlphaChar.Split("_");
                    if (items.Length > 2)
                    {
                        DateTime.TryParseExact(String.Format($"{items[0]}_{items[1]}"), dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                        if (result != DateTime.MinValue)
                            return result;
                    }
                    if (items.Length > 1)
                    {
                        DateTime.TryParseExact(String.Format($"{items[0]}"), dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                        if (result != DateTime.MinValue)
                            return result;
                    }
                }
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
