
namespace RFIDReaderAPI.Services
{
    public class RFIDMonitoringService : BackgroundService
    {
        private readonly RFIDReaderService _readerService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<RFIDMonitoringService> _logger;
        private readonly IConfiguration _configuration;
        private bool _wasLastStatusConnected = true;
        private DateTime _lastNotificationSent = DateTime.MinValue;

        public RFIDMonitoringService(
            RFIDReaderService readerService,
            INotificationService notificationService,
            ILogger<RFIDMonitoringService> logger,
            IConfiguration configuration)
        {
            _readerService = readerService;
            _notificationService = notificationService;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var status = _readerService.GetStatus();

                    if (_wasLastStatusConnected && !status.IsConnected)
                    {
                        if (DateTime.Now - _lastNotificationSent > TimeSpan.FromMinutes(5))
                        {
                            await _notificationService.SendNotificationAsync(
                                $"¡Alerta! El lector RFID se ha desconectado en {DateTime.Now}");
                            _lastNotificationSent = DateTime.Now;
                        }

                        try
                        {
                            _logger.LogInformation("Intentando reconectar el lector...");
                            await _readerService.StartReader();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error al intentar reconectar el lector");
                        }
                    }
                    else if (!_wasLastStatusConnected && status.IsConnected)
                    {
                        await _notificationService.SendNotificationAsync(
                            $"El lector RFID se ha reconectado exitosamente en {DateTime.Now}");
                        _lastNotificationSent = DateTime.Now;
                    }

                    _wasLastStatusConnected = status.IsConnected;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el monitoreo del lector RFID");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}