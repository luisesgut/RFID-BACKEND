// Services/EmailNotificationService.cs
using SendGrid;
using SendGrid.Helpers.Mail;
using Microsoft.Extensions.Options;
using RFIDReaderAPI.Configuration;

namespace RFIDReaderAPI.Services
{
    public class EmailNotificationService : INotificationService
    {
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly EmailSettings _emailSettings;
        private readonly SendGridClient _sendGridClient;

        public EmailNotificationService(
            ILogger<EmailNotificationService> logger,
            IOptions<EmailSettings> emailSettings)
        {
            _logger = logger;
            _emailSettings = emailSettings.Value;
            _sendGridClient = new SendGridClient(_emailSettings.ApiKey);
        }

        public async Task SendNotificationAsync(string message)
        {
            try
            {
                var from = new EmailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
                var to = new EmailAddress(_emailSettings.ToEmail, _emailSettings.ToName);
                var subject = $"Alerta Sistema RFID - {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                var htmlContent = $@"
                    <html>
                        <body>
                            <h2>Alerta del Sistema RFID</h2>
                            <p>{message}</p>
                            <hr>
                            <p style='font-size: 12px; color: #666;'>
                                Este es un mensaje automático del sistema de monitoreo RFID.
                                Por favor no responda a este correo.
                            </p>
                        </body>
                    </html>";

                var msg = MailHelper.CreateSingleEmail(
                    from,
                    to,
                    subject,
                    message, // texto plano
                    htmlContent // versión HTML
                );

                var response = await _sendGridClient.SendEmailAsync(msg);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Email enviado exitosamente: {subject}");
                }
                else
                {
                    var responseBody = await response.Body.ReadAsStringAsync();
                    _logger.LogWarning($"Error al enviar email. Status: {response.StatusCode}, Body: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar notificación por email");
            }
        }
    }
}