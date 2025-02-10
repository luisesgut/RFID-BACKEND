// Services/INotificationService.cs
namespace RFIDReaderAPI.Services
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string message);
    }
}