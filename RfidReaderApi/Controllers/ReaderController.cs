using Microsoft.AspNetCore.Mvc;
using System.Net.Mime;
using RFIDReaderAPI.Services;

namespace RFIDReaderAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces(MediaTypeNames.Application.Json)]
    public class ReaderController : ControllerBase
    {
        private readonly RFIDReaderService _readerService;
        private readonly ILogger<ReaderController> _logger;
        private readonly INotificationService _notificationService;


        public ReaderController(RFIDReaderService readerService, ILogger<ReaderController> logger, INotificationService notificationService)
        {
            _readerService = readerService ?? throw new ArgumentNullException(nameof(readerService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Inicia el lector RFID
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StartReader()
        {
            try
            {
                _logger.LogInformation("Iniciando lector RFID");
                await _readerService.StartReader();

                _logger.LogInformation("Lector RFID iniciado correctamente");

                await _notificationService.SendNotificationAsync(
                 $"El lector RFID se ha iniciado manualmente en {DateTime.Now}."
                );
                return Ok(new
                {
                    message = "Lector iniciado correctamente",
                    timestamp = DateTime.Now
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Intento de iniciar un lector ya iniciado");
                return BadRequest(new
                {
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no esperado al iniciar el lector");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error interno al iniciar el lector",
                    details = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Detiene el lector RFID
        /// </summary>
        [HttpPost("stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> StopReader()
        {
            try
            {
                _logger.LogInformation("Deteniendo lector RFID");
                await _readerService.StopReader();

                _logger.LogInformation("Lector RFID detenido correctamente");
                // Enviar notificación por correo
                await _notificationService.SendNotificationAsync(
                    $"El lector RFID se ha detenido manualmente en {DateTime.Now}."
                );
                return Ok(new
                {
                    message = "Lector detenido correctamente",
                    timestamp = DateTime.Now
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Intento de detener un lector ya detenido");
                return BadRequest(new
                {
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no esperado al detener el lector");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error interno al detener el lector",
                    details = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Obtiene el estado actual del lector RFID
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ResponseCache(Duration = 1)]
        public IActionResult GetStatus()
        {
            try
            {
                _logger.LogDebug("Consultando estado del lector");
                var status = _readerService.GetStatus();

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no esperado al obtener estado del lector");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error interno al obtener estado del lector",
                    details = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        /// <summary>
        /// Verifica el estado de salud del servicio del lector
        /// </summary>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public IActionResult HealthCheck()
        {
            try
            {
                var status = _readerService.GetStatus();
                if (status.IsConnected)
                {
                    return Ok(new
                    {
                        status = "Healthy",
                        timestamp = DateTime.Now
                    });
                }

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Unhealthy",
                    timestamp = DateTime.Now
                });
            }
            catch
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Unhealthy",
                    timestamp = DateTime.Now
                });
            }
        }


        [HttpPost("manage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ManageReader()
        {
            try
            {
                _logger.LogInformation("Gestionando el lector RFID...");

                // Verifica el estado del lector
                var status = _readerService.GetStatus();
                if (status.IsConnected)
                {
                    _logger.LogInformation("El lector está corriendo, deteniéndolo...");
                    await _readerService.StopReader();
                }

                _logger.LogInformation("Iniciando el lector...");
                await _readerService.StartReader();

                _logger.LogInformation("Lector gestionado correctamente");
                return Ok(new
                {
                    message = "Lector gestionado correctamente",
                    timestamp = DateTime.Now
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Error al gestionar el lector");
                return BadRequest(new
                {
                    error = ex.Message,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al gestionar el lector");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error interno al gestionar el lector",
                    details = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

    }
}