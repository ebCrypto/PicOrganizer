namespace PicOrganizer.Services
{
    public interface IDateRecognizerService
    {
        public DateTime InferDateFromName(string name);
    }
}