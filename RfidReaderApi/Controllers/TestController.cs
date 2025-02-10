using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using RFIDReaderAPI.Hubs;
using RFIDReaderAPI.Services;
using RfidReaderApi.Models;
using RfidReaderApi.Services;


[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IHubContext<ReaderHub> _hubContext;
    private readonly INotificationService _notificationService;
    private readonly IProductDataService _productDataService;


    public TestController(IHubContext<ReaderHub> hubContext, INotificationService notificationService, IProductDataService productDataService)
    {
        _hubContext = hubContext;
        _notificationService = notificationService;
        _productDataService = productDataService; // Asignar la instancia inyectada
    }

    [HttpPost("send-test-message")]
    public async Task<IActionResult> SendTestMessage([FromBody] object message)
    {
        try
        {
            if (message == null)
            {
                return BadRequest(new { error = "El mensaje no puede ser nulo." });
            }

            var jsonMessage = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(message.ToString());

            if (!jsonMessage.ContainsKey("Type"))
            {
                return BadRequest(new { error = "El tipo de mensaje ('Type') es obligatorio." });
            }

            string messageType = jsonMessage["Type"].ToString();

            await _hubContext.Clients.All.SendAsync(messageType, jsonMessage);

            return Ok(new { status = "Mensaje enviado exitosamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("send-test-email")]
    public async Task<IActionResult> SendTestEmail()
    {
        try
        {
            string testMessage = "Este es un correo de prueba enviado desde el sistema RFID.";
            await _notificationService.SendNotificationAsync(testMessage);

            return Ok(new { status = "Correo de prueba enviado exitosamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }


    [HttpPost("test-association")]
    public async Task<IActionResult> TestAssociation([FromBody] TestAssociationRequest request)
    {
        try
        {
            // Validar los EPCs proporcionados
            if (string.IsNullOrEmpty(request.PalletEpc) || string.IsNullOrEmpty(request.OperatorEpc))
            {
                return BadRequest(new { error = "Los EPCs de tarima y operador son obligatorios." });
            }

            // Obtener información de la tarima
            var productData = await _productDataService.GetProductDataAsync(request.PalletEpc);
            if (productData == null)
            {
                return NotFound(new { error = $"No se encontró información para la tarima con EPC: {request.PalletEpc}" });
            }

            // Obtener información del operador
            var operatorData = await _productDataService.GetOperatorInfoAsync(request.OperatorEpc);
            if (operatorData == null)
            {
                return NotFound(new { error = $"No se encontró información para el operador con EPC: {request.OperatorEpc}" });
            }

            // Retornar ambos datos
            return Ok(new
            {
                status = "Datos obtenidos exitosamente.",
                product = productData,
                operatorData = operatorData // Renombrado para evitar conflictos
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("simulate-association")]
    public async Task<IActionResult> SimulateAssociation([FromBody] TestAssociationRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.PalletEpc))
            {
                return BadRequest(new { error = "El EPC de la tarima es obligatorio." });
            }

            // Obtener información de la tarima
            var productData = await _productDataService.GetProductDataAsync(request.PalletEpc);
            if (productData == null)
            {
                return NotFound(new { error = $"No se encontró información para la tarima con EPC: {request.PalletEpc}" });
            }

            // Registrar información extra para la tarima (siempre se ejecuta)
            await _productDataService.RegisterExtraInfoAsync(request.PalletEpc);

            // Registrar antena con información del operador o como "Indefinido"
            var operatorEpc = string.IsNullOrEmpty(request.OperatorEpc) ? "Indefinido" : request.OperatorEpc;
            await _productDataService.RegisterAntennaRecordAsync(request.PalletEpc, operatorEpc);

            if (string.IsNullOrEmpty(request.OperatorEpc))
            {
                // Si el operador es indefinido, enviar evento de tarima sin operador
                await _hubContext.Clients.All.SendAsync("NewPallet", new
                {
                    Product = productData,
                    Rssi = -50.5,
                    AntennaPort = 1,
                    Timestamp = DateTime.Now,
                    Success = true
                });

                return Ok(new { status = "Tarima enviada sin operador." });
            }

            // Obtener información del operador si existe
            var operatorData = await _productDataService.GetOperatorInfoAsync(request.OperatorEpc);
            if (operatorData == null)
            {
                return NotFound(new { error = $"No se encontró información para el operador con EPC: {request.OperatorEpc}" });
            }

            // Enviar evento de asociación
            await _hubContext.Clients.All.SendAsync("NewAssociation", new
            {
                Product = productData,
                OperatorInfo = operatorData,
                Rssi = -50.5,
                AntennaPort = 1,
                Timestamp = DateTime.Now,
                Success = true
            });

            return Ok(new { status = "Asociación enviada exitosamente." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }






}
