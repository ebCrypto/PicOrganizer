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
        private readonly ILogger<DateRecognizerService> logger;
        private readonly AppSettings appSettings;

        public DateRecognizerService(ILogger<DateRecognizerService> logger, AppSettings appSettings)
        {
            this.logger = logger;
            this.appSettings = appSettings;
        }

        private DateTime InferDateFromWhatsappName(string name)
        {
            var regex = new Regex(appSettings.WhatsappNameRegex);
            if (regex.IsMatch(name))
            {
                DateTime.TryParseExact(name.Substring(4, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                return result;
            }
            else
                return DateTime.MinValue;
        }

        public DateTime InferDateFromName(string name)
        {
            if (name.Contains('.'))
                name = name.Substring(0,name.LastIndexOf("."));
            var whatsapp = InferDateFromWhatsappName(name);
            if (Valid(whatsapp))
                return whatsapp;
            string noAlphaChar = Regex.Replace(name, "[^0-9_-]", "");            
            foreach (var dateFormat in appSettings.KnownUsedDateFormatsInNames)
            {

                DateTime.TryParseExact(noAlphaChar, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                if (Valid(result))
                    return result;
            }
            noAlphaChar = noAlphaChar.Replace("-", "_");            
            foreach (var dateFormat in appSettings.KnownUsedDateFormatsInNames)
            {
                if (noAlphaChar.Contains('_'))
                {
                    var items = noAlphaChar.Split("_");
                    if (items.Length > 2)
                    {
                        DateTime.TryParseExact(String.Format($"{items[0]}_{items[1]}"), dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                        if (Valid(result))
                            return result;
                    }
                    if (items.Length > 1)
                    {
                        DateTime.TryParseExact(String.Format($"{items[0]}"), dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
                        if (Valid(result))
                            return result;
                    }
                }
            }

            List<ModelResult>? modelResults = DateTimeRecognizer.RecognizeDateTime(name, appSettings.InputSettings.Culture);
            if (modelResults.Any())
            {
                foreach (var modelResult in modelResults)
                {
                    var resolution = modelResult.Resolution;
                    if(resolution == null)
                    {
                        logger.LogDebug("No Resolution for {@ModelResult} in {Name}", modelResult, name);
                        return DateTime.MinValue;
                    }
                    logger.LogDebug("Found {Count} option(s) while attempting date resolution for name {Name}", resolution.Count(), name);
                    foreach (KeyValuePair<string, object> resolutionValue in resolution)
                    {
                        var value = (List<Dictionary<string, string>>)resolutionValue.Value;
                        logger.LogTrace("Found {Count} value(s) in this resolution for name {Name}", value.Count, name);
                        DateTime.TryParse(value?[0]?["timex"], out var result);
                        if (result.Year >= appSettings.InputSettings.StartingYearOfLibrary && result < DateTime.Now)
                        {
                            logger.LogDebug("Inferring DateTaken '{Date}' from name {Name}", result.ToString(), name);
                            return result;
                        }
                        else
                            logger.LogTrace("Declaring {Date} invalid because it is not within the range [year={Start} -> {End}]", result.ToShortDateString(), appSettings.InputSettings.StartingYearOfLibrary, DateTime.Now.ToShortDateString());
                    }
                }
            }
            return DateTime.MinValue;
        }

        public bool Valid(DateTime value)
        {
            return value != DateTime.MinValue && value.Year >= appSettings.InputSettings.StartingYearOfLibrary && value.Year <= DateTime.Today.Year;
        }
    }
}
