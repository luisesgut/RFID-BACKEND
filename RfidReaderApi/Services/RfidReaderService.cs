using Impinj.OctaneSdk;
using Microsoft.AspNetCore.SignalR;
using RfidReaderApi.Services;
using RFIDReaderAPI.Hubs;
using System.Collections.Concurrent;

namespace RFIDReaderAPI.Services
{
    public class RFIDReaderService
    {
        private readonly ImpinjReader reader;
        private readonly IHubContext<ReaderHub> hubContext;
        private readonly IProductDataService _productService;
        private readonly object lockObject = new object();
        private bool isConnected = false;
        private readonly ConcurrentDictionary<string, PalletInfo> pendingPallets;
        private readonly ConcurrentDictionary<string, string> palletOperatorAssociations;
        private const string READER_IP = "172.16.100.198";
        private const int ASSOCIATION_TIMEOUT_SECONDS = 3   ;
        private const int RECONNECTION_ATTEMPT_DELAY_MS = 5000; // 5 segundos entre intentos
        private const int MAX_RECONNECTION_ATTEMPTS = 5;
        private Settings lastKnownSettings;
        private bool isReconnecting = false;
        private System.Timers.Timer reconnectionTimer;
        private System.Timers.Timer processingTimer;
        private int reconnectionAttempts = 0;
        //keep alives
        private System.Timers.Timer keepAliveTimer;
        private const int KEEP_ALIVE_INTERVAL_MS = 20000; // 30 segundos
        //diccionario para etiquetas procesadas recientemente para onTagsReported
        private readonly ConcurrentDictionary<string, DateTime> recentlyProcessedTags;
        private const int TAG_COOLDOWN_SECONDS = 5; // Tiempo de enfriamiento entre lecturas
        //funcion limpiar timers y diccionarios
        private System.Timers.Timer cleanupTimer;
        private const int CLEANUP_INTERVAL_MS = 60000; // Limpiar cada minuto

        private class PalletInfo
        {
            public DateTime DetectedTime { get; set; }
            public double Rssi { get; set; }
            public ushort AntennaPort { get; set; }
            public bool IsProcessed { get; set; }
        }

        public class ReaderStatus
        {
            public bool IsConnected { get; set; }
            public bool IsReconnecting { get; set; }
            public int ReconnectionAttempts { get; set; }
            public string Message { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public RFIDReaderService(IHubContext<ReaderHub> hubContext,IProductDataService productService)
        {
            this.hubContext = hubContext;
            _productService = productService;
            reader = new ImpinjReader();
            pendingPallets = new ConcurrentDictionary<string, PalletInfo>();
            palletOperatorAssociations = new ConcurrentDictionary<string, string>();

            // Configurar timer de reconexión
            reconnectionTimer = new System.Timers.Timer(RECONNECTION_ATTEMPT_DELAY_MS);
            reconnectionTimer.Elapsed += async (sender, e) => await AttemptReconnection();
            reconnectionTimer.AutoReset = false;

            // Configurar timer de procesamiento de pallets
            processingTimer = new System.Timers.Timer(1000); // Revisar cada segundo
            processingTimer.Elapsed += async (sender, e) => await ProcessPendingPallets();
            processingTimer.Start();

            // Configurar timer de keep-alive
            keepAliveTimer = new System.Timers.Timer(KEEP_ALIVE_INTERVAL_MS);
            keepAliveTimer.Elapsed += async (sender, e) => await SendKeepAlive();
            keepAliveTimer.Start();
            //evitar duplicados
            recentlyProcessedTags = new ConcurrentDictionary<string, DateTime>();
            // Configurar timer de limpieza
            cleanupTimer = new System.Timers.Timer(CLEANUP_INTERVAL_MS);
            cleanupTimer.Elapsed += CleanupOldEntries;
            cleanupTimer.Start();
        }

        private async Task SendKeepAlive()
        {
            var status = GetStatus();
            await hubContext.Clients.All.SendAsync("KeepAlive", new
            {
                status.IsConnected,
                status.IsReconnecting,
                status.ReconnectionAttempts,
                status.Message,
                Timestamp = DateTime.Now
            });
        }

        private async Task ProcessPendingPallets()
        {
            var now = DateTime.Now;
            var expiredPallets = pendingPallets
                .Where(p => !p.Value.IsProcessed &&
                           (now - p.Value.DetectedTime).TotalSeconds >= ASSOCIATION_TIMEOUT_SECONDS)
                .ToList();

            foreach (var pallet in expiredPallets)
            {
                if (pendingPallets.TryGetValue(pallet.Key, out var palletInfo))
                {
                    try
                    {
                        // Obtener información del producto
                        var productData = await _productService.GetProductDataAsync(pallet.Key);

                        // Actualizar campos específicos para tarima sin operador
                        productData.Operator = "Indefinido";

                        // Realizar actualizaciones en paralelo
                        var updateTasks = new List<Task>
                {
                    _productService.UpdateStatusAsync(pallet.Key, 2),
                    _productService.RegisterExtraInfoAsync(pallet.Key)
                };

                        await Task.WhenAll(updateTasks);

                        palletInfo.IsProcessed = true;

                        // Notificar con toda la información del producto
                        await hubContext.Clients.All.SendAsync("NewPallet", new
                        {
                            Product = productData,
                            Rssi = palletInfo.Rssi,
                            AntennaPort = palletInfo.AntennaPort,
                            Timestamp = DateTime.Now,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        palletInfo.IsProcessed = true;
                        await hubContext.Clients.All.SendAsync("NewPallet", new
                        {
                            Epc = pallet.Key,
                            Rssi = palletInfo.Rssi,
                            AntennaPort = palletInfo.AntennaPort,
                            Success = false,
                            Error = ex.Message,
                            Timestamp = DateTime.Now
                        });
                    }
                }
            }
        }


        private async Task AttemptReconnection()
        {
            if (!isReconnecting || reconnectionAttempts >= MAX_RECONNECTION_ATTEMPTS) return;

            try
            {
                reconnectionAttempts++;
                await hubContext.Clients.All.SendAsync("ReaderStatus", new
                {
                    status = $"Intentando reconexión ({reconnectionAttempts}/{MAX_RECONNECTION_ATTEMPTS})",
                    timestamp = DateTime.Now
                });

                reader.Connect(READER_IP);

                if (lastKnownSettings != null)
                {
                    reader.ApplySettings(lastKnownSettings);
                }
                else
                {
                    await ConfigureReaderSettings();
                }

                reader.TagsReported += OnTagsReported;
                reader.ConnectionLost += OnConnectionLost;
                reader.Start();

                isConnected = true;
                isReconnecting = false;
                reconnectionAttempts = 0;

                await hubContext.Clients.All.SendAsync("ReaderStatus", new
                {
                    status = "Reconnected",
                    timestamp = DateTime.Now
                });
            }
            catch (OctaneSdkException ex)
            {
                await hubContext.Clients.All.SendAsync("ReaderError", new
                {
                    error = $"Error de reconexión: {ex.Message}",
                    attempt = reconnectionAttempts,
                    timestamp = DateTime.Now
                });

                if (reconnectionAttempts < MAX_RECONNECTION_ATTEMPTS)
                {
                    reconnectionTimer.Start();
                }
                else
                {
                    isReconnecting = false;
                    await hubContext.Clients.All.SendAsync("ReaderStatus", new
                    {
                        status = "ReconnectionFailed",
                        timestamp = DateTime.Now
                    });
                }
            }
        }
        //limpiar timers duplicados
        private void CleanupOldEntries(object sender, System.Timers.ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var oldEntries = recentlyProcessedTags
                .Where(kvp => (now - kvp.Value).TotalSeconds >= TAG_COOLDOWN_SECONDS)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in oldEntries)
            {
                recentlyProcessedTags.TryRemove(key, out _);
            }
        }

        private async Task ConfigureReaderSettings()
        {
            Settings settings = reader.QueryDefaultSettings();

            // Configuración optimizada para portal RFID
            settings.Report.Mode = ReportMode.Individual; // Usamos modo individual para máxima responsividad

            // Configuración de reporte
            settings.Report.IncludeAntennaPortNumber = true;
            settings.Report.IncludePeakRssi = true;
            settings.Report.IncludePhaseAngle = true;

            // Configuración crucial para portal
            settings.Session = 2; // Session 2 es mejor para portales
            settings.SearchMode = SearchMode.DualTarget; // Mejor para detectar múltiples tags
            settings.TagPopulationEstimate = 8; // Ajustado para tus 2 tags (producto + operador) con margen

            //// Desactivamos filtros de duplicados para asegurar lecturas
            //settings.Report.FilterDuplicatesMode = FilterDuplicatesMode.None;

            // Configuración de antenas optimizada para portal
            settings.Antennas.DisableAll();
            settings.Antennas.EnableAll();

            // Configuración por antena
            for (ushort antennaPort = 1; antennaPort <= 13; antennaPort++)
            {
                var antenna = settings.Antennas.GetAntenna(antennaPort);
                antenna.IsEnabled = true;

                // Máxima potencia debido a las cortinas hawaianas
                antenna.TxPowerInDbm = 28; // Máxima potencia permitida
                antenna.RxSensitivityInDbm = -65.0; // Alta sensibilidad

                // Configuraciones específicas para portal
                antenna.MaxRxSensitivity = true;
                antenna.MaxTxPower = true;
            }

            lastKnownSettings = settings;
            reader.ApplySettings(settings);
        }

        public async Task StartReader()
        {
            try
            {
                if (isConnected)
                {
                    throw new InvalidOperationException("El lector ya está iniciado");
                }

                reader.Connect(READER_IP);
                await ConfigureReaderSettings();

                reader.TagsReported += OnTagsReported;
                reader.ConnectionLost += OnConnectionLost;

                reader.Start();
                isConnected = true;

                await hubContext.Clients.All.SendAsync("ReaderStatus", new { status = "Started", timestamp = DateTime.Now });
            }
            catch (OctaneSdkException ex)
            {
                await hubContext.Clients.All.SendAsync("ReaderError", new { error = ex.Message, timestamp = DateTime.Now });
                throw;
            }
        }

        private async void OnTagsReported(ImpinjReader sender, TagReport report)
        {
            foreach (Tag tag in report.Tags)
            {
                string epc = tag.Epc.ToHexString();
                double rssiAvg = tag.PeakRssiInDbm; // Usar el valor correcto del RSSI promedio si está disponible

                // Umbral de RSSI Avg para filtrar etiquetas que realmente están pasando bajo la antena
                if (rssiAvg < -60)
                {
                    continue; // Ignorar EPCs con señal baja (lejos de la antena)
                }

                // Verificar si la etiqueta está en período de cooldown
                if (!ShouldProcessTag(epc))
                {
                    continue;
                }

                try
                {
                    if (epc.Length == 16) // Tarima
                    {
                        if (!pendingPallets.ContainsKey(epc))
                        {
                            var palletInfo = new PalletInfo
                            {
                                DetectedTime = DateTime.Now,
                                Rssi = rssiAvg, // Guardar el RSSI Avg
                                AntennaPort = tag.AntennaPortNumber,
                                IsProcessed = false
                            };

                            pendingPallets.TryAdd(epc, palletInfo);

                            // Opcionalmente, podrías pre-cargar los datos del producto aquí
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    await _productService.GetProductDataAsync(epc);
                                }
                                catch
                                {
                                    // Ignorar errores en la pre-carga
                                }
                            });
                        }
                    }
                    else if (epc.Length == 12) // Operador
                    {
                        var palletsToAssociate = pendingPallets
                            .Where(p => !p.Value.IsProcessed &&
                                        (DateTime.Now - p.Value.DetectedTime).TotalSeconds < ASSOCIATION_TIMEOUT_SECONDS)
                            .ToList();

                        foreach (var palletPair in palletsToAssociate)
                        {
                            try
                            {
                                var productTask = _productService.GetProductDataAsync(palletPair.Key);
                                var operatorTask = _productService.GetOperatorInfoAsync(epc);

                                await Task.WhenAll(productTask, operatorTask);

                                var productData = await productTask;
                                var operatorInfo = await operatorTask;

                                productData.Operator = operatorInfo?.NombreOperador ?? "Indefinido";

                                var updateTasks = new List<Task>
                        {
                            _productService.UpdateStatusAsync(palletPair.Key, 2),
                            _productService.RegisterExtraInfoAsync(palletPair.Key),
                            _productService.RegisterAntennaRecordAsync(palletPair.Key, epc)
                        };

                                await Task.WhenAll(updateTasks);

                                palletPair.Value.IsProcessed = true;
                                palletOperatorAssociations.TryAdd(palletPair.Key, epc);

                                await hubContext.Clients.All.SendAsync("NewAssociation", new
                                {
                                    Product = productData,
                                    OperatorInfo = operatorInfo,
                                    Rssi = palletPair.Value.Rssi,
                                    AntennaPort = palletPair.Value.AntennaPort,
                                    Timestamp = DateTime.Now,
                                    Success = true
                                });
                            }
                            catch (Exception ex)
                            {
                                await hubContext.Clients.All.SendAsync("NewAssociation", new
                                {
                                    PalletEpc = palletPair.Key,
                                    OperatorEpc = epc,
                                    Rssi = palletPair.Value.Rssi,
                                    AntennaPort = palletPair.Value.AntennaPort,
                                    Success = false,
                                    Error = ex.Message,
                                    Timestamp = DateTime.Now
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    await hubContext.Clients.All.SendAsync("ReaderError", new
                    {
                        Error = $"Error processing tag {epc}: {ex.Message}",
                        Timestamp = DateTime.Now
                    });
                }
            }
        }

        private bool ShouldProcessTag(string epc)
        {
            var now = DateTime.Now;

            // Si la etiqueta no está en el diccionario o ha pasado el tiempo de cooldown
            if (!recentlyProcessedTags.TryGetValue(epc, out DateTime lastProcessed) ||
                (now - lastProcessed).TotalSeconds >= TAG_COOLDOWN_SECONDS)
            {
                // Actualizar o agregar el timestamp de la última vez que se procesó
                recentlyProcessedTags.AddOrUpdate(epc, now, (key, oldValue) => now);
                return true;
            }

            return false;
        }

        private async void OnConnectionLost(ImpinjReader reader)
        {
            isConnected = false;
            await hubContext.Clients.All.SendAsync("ReaderStatus", new
            {
                status = "ConnectionLost",
                timestamp = DateTime.Now
            });

            // Iniciar proceso de reconexión
            if (!isReconnecting)
            {
                isReconnecting = true;
                reconnectionAttempts = 0;
                reconnectionTimer.Start();
            }
        }

        public async Task StopReader()
        {
            try
            {
                if (!isConnected)
                {
                    throw new InvalidOperationException("El lector no está iniciado");
                }

                isReconnecting = false; // Detener cualquier intento de reconexión
                reconnectionTimer.Stop();

                reader.Stop();
                reader.Disconnect();
                isConnected = false;
                await hubContext.Clients.All.SendAsync("ReaderStatus", new { status = "Stopped", timestamp = DateTime.Now });
            }
            catch (Exception ex)
            {
                await hubContext.Clients.All.SendAsync("ReaderError", new { error = ex.Message, timestamp = DateTime.Now });
                throw;
            }
        }

        public ReaderStatus GetStatus()
        {
            if (!isConnected)
            {
                return new ReaderStatus
                {
                    IsConnected = false,
                    IsReconnecting = isReconnecting,
                    ReconnectionAttempts = reconnectionAttempts,
                    Message = isReconnecting ? "Intentando reconectar" : "Lector desconectado",
                    Timestamp = DateTime.Now
                };
            }

            try
            {
                var status = reader.QueryStatus();
                return new ReaderStatus
                {
                    IsConnected = true,
                    IsReconnecting = false,
                    ReconnectionAttempts = 0,
                    Message = "Lector conectado",
                    Timestamp = DateTime.Now
                };
            }
            catch (Exception)
            {
                isConnected = false;
                return new ReaderStatus
                {
                    IsConnected = false,
                    IsReconnecting = false,
                    ReconnectionAttempts = reconnectionAttempts,
                    Message = "Error al obtener estado",
                    Timestamp = DateTime.Now
                };
            }
        }


        public void Dispose()
        {
            reconnectionTimer?.Dispose();
            processingTimer?.Dispose();
            keepAliveTimer?.Dispose();
            cleanupTimer?.Dispose();
            if (isConnected)
            {
                try
                {
                    reader.Stop();
                    reader.Disconnect();
                }
                catch
                {
                    // Ignorar errores durante la disposición
                }
            }
        }
    }
}