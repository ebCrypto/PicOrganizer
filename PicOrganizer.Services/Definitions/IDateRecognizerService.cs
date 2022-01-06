namespace PicOrganizer.Services
{
    public interface IDateRecognizerService
    {
        DateTime InferDateFromName(string name);
        bool Valid(DateTime result);
    }
}