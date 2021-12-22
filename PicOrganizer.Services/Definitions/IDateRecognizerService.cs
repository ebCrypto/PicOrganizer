namespace PicOrganizer.Services
{
    public interface IDateRecognizerService
    {
        public Task<DateTime> InferDateFromName(string name);
    }
}