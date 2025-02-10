using Microsoft.AspNetCore.SignalR;
using RfidReaderApi.Models;
using RfidReaderApi.Services;
using System.Text.Json;

namespace RFIDReaderAPI.Hubs
{
    public class ReaderHub : Hub
    {
        private readonly IProductDataService _productService;
        private readonly ILogger<ReaderHub> _logger;
        private static bool _readerConnected;
        private static DateTime _lastKeepAlive = DateTime.Now;

        public ReaderHub(IProductDataService productService, ILogger<ReaderHub> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message);
        }

        public async Task SendKeepAlive(bool isConnected)
        {
            _readerConnected = isConnected;
            _lastKeepAlive = DateTime.Now;
            await Clients.All.SendAsync("ReaderStatus", new
            {
                IsConnected = _readerConnected,
                LastKeepAlive = _lastKeepAlive
            });
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("ReaderStatus", new
            {
                IsConnected = _readerConnected,
                LastKeepAlive = _lastKeepAlive
            });
            await Clients.Caller.SendAsync("ReceiveMessage", "Conectado al hub del lector RFID");
            await base.OnConnectedAsync();
        }

        private bool ValidateProductData(ProductInfo product)
        {
            return product != null &&
                   !string.IsNullOrEmpty(product.Id) &&
                   !string.IsNullOrEmpty(product.Epc) &&
                   !string.IsNullOrEmpty(product.NetWeight) &&
                   !string.IsNullOrEmpty(product.Pieces) &&
                   !string.IsNullOrEmpty(product.UnitOfMeasure);
        }

        public async Task NotifyNewAssociation(string palletEpc, string operatorEpc, double rssi, ushort antennaPort)
        {
            try
            {
                // Obtener información del producto y operador en paralelo
                var productTask = _productService.GetProductDataAsync(palletEpc);
                var operatorTask = _productService.GetOperatorInfoAsync(operatorEpc);

                await Task.WhenAll(productTask, operatorTask);

                var product = await productTask;
                var operatorInfo = await operatorTask;

                // Asignar operador al producto
                product.Operator = operatorInfo?.NombreOperador ?? "Indefinido";

                // Validar datos del producto
                if (!ValidateProductData(product))
                {
                    throw new Exception($"Datos de producto inválidos o incompletos para EPC: {palletEpc}");
                }

                // Realizar actualizaciones en paralelo
                var updateTasks = new List<Task>
                {
                    _productService.UpdateStatusAsync(palletEpc, 2),
                    _productService.RegisterExtraInfoAsync(palletEpc),
                    _productService.RegisterAntennaRecordAsync(palletEpc, operatorEpc)
                };

                await Task.WhenAll(updateTasks);

                var message = new
                {
                    Success = true,
                    Product = new
                    {
                        Id = product.Id ?? string.Empty,
                        Name = product.Name ?? "Producto sin nombre",
                        Epc = product.Epc ?? string.Empty,
                        Status = product.Status ?? "pending",
                        ImageUrl = product.ImageUrl ?? "https://calibri.mx/bioflex/wp-content/uploads/2024/03/standup_pouch.png",
                        NetWeight = product.NetWeight ?? "N/A",
                        Pieces = product.Pieces ?? "N/A",
                        UnitOfMeasure = product.UnitOfMeasure ?? "N/A",
                        PrintCard = product.PrintCard ?? "N/A",
                        Operator = product.Operator ?? "Indefinido",
                        TipoEtiqueta = product.TipoEtiqueta ?? "N/A",
                        Area = product.Area ?? "N/A",
                        ClaveProducto = product.ClaveProducto ?? "N/A",
                        PesoBruto = product.PesoBruto ?? "N/A",
                        PesoTarima = product.PesoTarima ?? "N/A",
                        FechaEntrada = product.FechaEntrada ,
                        HoraEntrada = product.HoraEntrada ,
                        Rfid = product.Rfid ?? string.Empty
                    },
                    OperatorInfo = operatorInfo != null
                        ? new
                        {
                            NombreOperador = operatorInfo.NombreOperador ?? "Indefinido",
                            Area = operatorInfo.Area ?? "N/A",
                            FechaAlta = operatorInfo.FechaAlta,
                        }
                        : null,
                    Rssi = rssi,
                    AntennaPort = antennaPort,
                    Timestamp = DateTime.Now.ToString("o")
                };

                _logger.LogInformation("Enviando asociación: {Message}", JsonSerializer.Serialize(message));
                await Clients.All.SendAsync("NewAssociation", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing association for Pallet: {PalletEpc}, Operator: {OperatorEpc}",
                    palletEpc, operatorEpc);

                await Clients.All.SendAsync("NewAssociation", new
                {
                    PalletEpc = palletEpc,
                    OperatorEpc = operatorEpc,
                    Success = false,
                    Error = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }

        public async Task NotifyNewPallet(string palletEpc, double rssi, ushort antennaPort)
        {
            try
            {
                var product = await _productService.GetProductDataAsync(palletEpc);

                // Validar datos del producto
                if (!ValidateProductData(product))
                {
                    throw new Exception($"Datos de producto inválidos o incompletos para EPC: {palletEpc}");
                }

                product.Name = "Tarima sin operador";
                product.Operator = "Indefinido";

                // Realizar actualizaciones en paralelo
                var updateTasks = new List<Task>
                {
                    _productService.UpdateStatusAsync(palletEpc, 2),
                    _productService.RegisterExtraInfoAsync(palletEpc)
                };

                await Task.WhenAll(updateTasks);

                var message = new
                {
                    Success = true,
                    Product = new
                    {
                        Id = product.Id ?? string.Empty,
                        Name = product.Name ?? "Tarima sin operador",
                        Epc = product.Epc ?? string.Empty,
                        Status = "success",
                        ImageUrl = product.ImageUrl ?? "https://calibri.mx/bioflex/wp-content/uploads/2024/03/standup_pouch.png",
                        NetWeight = product.NetWeight ?? "N/A",
                        Pieces = product.Pieces ?? "N/A",
                        UnitOfMeasure = product.UnitOfMeasure ?? "N/A",
                        PrintCard = product.PrintCard ?? "N/A",
                        Operator = "Indefinido",
                        TipoEtiqueta = product.TipoEtiqueta ?? "N/A",
                        Area = product.Area ?? "N/A",
                        ClaveProducto = product.ClaveProducto ?? "N/A",
                        PesoBruto = product.PesoBruto ?? "N/A",
                        PesoTarima = product.PesoTarima ?? "N/A",
                        FechaEntrada = product.FechaEntrada ,
                        HoraEntrada = product.HoraEntrada ,
                        Rfid = product.Rfid ?? string.Empty
                    },
                    Rssi = rssi,
                    AntennaPort = antennaPort,
                    Timestamp = DateTime.Now.ToString("o")
                };

                _logger.LogInformation("Enviando nueva tarima: {Message}", JsonSerializer.Serialize(message));
                await Clients.All.SendAsync("NewPallet", message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing new pallet: {Epc}", palletEpc);

                await Clients.All.SendAsync("NewPallet", new
                {
                    PalletEpc = palletEpc,
                    Success = false,
                    Error = ex.Message,
                    Timestamp = DateTime.Now
                });
            }
        }
    }
}
