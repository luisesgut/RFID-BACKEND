// Configuration/EmailSettings.cs
namespace RFIDReaderAPI.Configuration
{
    public class EmailSettings
    {
        public string ApiKey { get; set; }
        public string FromEmail { get; set; }
        public string FromName { get; set; }
        public string ToEmail { get; set; }
        public string ToName { get; set; }
    }
}