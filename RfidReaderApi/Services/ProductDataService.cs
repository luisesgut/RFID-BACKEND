using RfidReaderApi.Models;
using RfidReaderApi.Exceptions;
using System.Net;

namespace RfidReaderApi.Services
{
    public class ProductDataService : IProductDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProductDataService> _logger;

        public ProductDataService(HttpClient httpClient, ILogger<ProductDataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<ProductInfo> GetProductDataAsync(string epc)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://172.16.10.31/api/socket/{epc}");
                response.EnsureSuccessStatusCode();

                // Deserializar la respuesta como ProductResponse
                var data = await response.Content.ReadFromJsonAsync<ProductResponse>();

                // Asegurarse de que la respuesta no sea nula
                if (data == null)
                {
                    throw new Exception($"No se pudo deserializar la respuesta para el EPC: {epc}");
                }

                // Mapear los datos al modelo interno ProductInfo
                return new ProductInfo
                {
                    Id = epc,
                    Name = data.NombreProducto ?? "Producto sin nombre",
                    Epc = epc,
                    Status = "pending",
                    ImageUrl = data.UrlImagen ?? "https://calibri.mx/bioflex/wp-content/uploads/2024/03/standup_pouch.png",
                    NetWeight = data.PesoNeto ?? "N/A",
                    Pieces = data.Piezas ?? "N/A",
                    UnitOfMeasure = data.Uom ?? "N/A",
                    PrintCard = data.ProductPrintCard ?? "N/A",
                    TipoEtiqueta = data.TipoEtiqueta ?? "N/A",
                    Area = data.Area ?? "N/A",
                    ClaveProducto = data.ClaveProducto ?? "N/A",
                    PesoBruto = data.PesoBruto ?? "N/A",
                    PesoTarima = data.PesoTarima ?? "N/A",
                    FechaEntrada = DateTime.Now,
                    HoraEntrada = DateTime.Now.TimeOfDay,
                    Rfid = data.Rfid ?? epc,
                    TipoProducto = data.TipoProducto ?? "N/A"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product data for EPC: {Epc}", epc);
                throw;
            }
        }


        public async Task<OperatorInfo> GetOperatorInfoAsync(string epcOperador)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://172.16.10.31/api/OperadoresRFID/{epcOperador}");

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Operador no encontrado: {EpcOperador}", epcOperador);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var operatorInfo = await response.Content.ReadFromJsonAsync<OperatorInfo>();

                if (operatorInfo == null)
                {
                    _logger.LogWarning("Datos de operador inválidos: {EpcOperador}", epcOperador);
                    return null;
                }

                return operatorInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del operador: {EpcOperador}", epcOperador);
                return null;
            }
        }

        public async Task UpdateStatusAsync(string epc, int newStatus)
        {
            try
            {
                var response = await _httpClient.PutAsJsonAsync(
                    $"http://172.16.10.31/api/RfidLabel/UpdateStatusByRFID/{epc}",
                    new { status = newStatus }
                );

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new ProductDataException(
                        $"El EPC {epc} ya está registrado con otro estado",
                        epc,
                        ProductErrorType.AlreadyRegistered);
                }

                response.EnsureSuccessStatusCode();
            }
            catch (ProductDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar estado para EPC: {Epc}", epc);
                throw new ProductDataException(
                    $"Error al actualizar estado del producto {epc}",
                    epc,
                    ProductErrorType.Unknown,
                    ex);
            }
        }

        public async Task RegisterExtraInfoAsync(string epc)
        {
            try
            {
                _logger.LogInformation("Iniciando POST a http://172.16.10.31/api/ProdExtraInfo/EntradaAlmacen/{epc}", epc);

                var response = await _httpClient.PostAsJsonAsync(
                    $"http://172.16.10.31/api/ProdExtraInfo/EntradaAlmacen/{epc}",
                    new { }
                );

                _logger.LogInformation("Respuesta recibida para EPC {epc}: {StatusCode}", epc, response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new ProductDataException(
                        $"La información extra para el EPC {epc} ya está registrada",
                        epc,
                        ProductErrorType.AlreadyRegistered);
                }

                response.EnsureSuccessStatusCode();
            }
            catch (ProductDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar información extra para EPC: {Epc}", epc);
                throw new ProductDataException(
                    $"Error al registrar información extra para {epc}",
                    epc,
                    ProductErrorType.Unknown,
                    ex);
            }
        }

        public async Task RegisterAntennaRecordAsync(string epc, string? epcOperador)
        {
            try
            {
                epcOperador ??= "Indefinido";
                _logger.LogInformation(
                    "Iniciando POST a http://172.16.10.31/api/ProdRegistroAntenas con epcOperador={epcOperador} y epc={epc}",
                    epcOperador, epc);

                var response = await _httpClient.PostAsJsonAsync(
                    $"http://172.16.10.31/api/ProdRegistroAntenas?epcOperador={epcOperador}&epc={epc}",
                    new { }
                );

                _logger.LogInformation("Respuesta recibida para EPC {epc} y operador {epcOperador}: {StatusCode}", epc, epcOperador, response.StatusCode);

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new ProductDataException(
                        $"El registro de antena para el EPC {epc} ya existe",
                        epc,
                        ProductErrorType.AlreadyRegistered);
                }

                response.EnsureSuccessStatusCode();
            }
            catch (ProductDataException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar antena para EPC: {Epc}", epc);
                throw new ProductDataException(
                    $"Error al registrar antena para {epc}",
                    epc,
                    ProductErrorType.Unknown,
                    ex);
            }
        }

    }
}