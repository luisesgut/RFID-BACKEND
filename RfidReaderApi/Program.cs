using RFIDReaderAPI.Hubs;
using RFIDReaderAPI.Services;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using RFIDReaderAPI.Configuration;
using RfidReaderApi.Exceptions;
using RfidReaderApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RFID Reader API",
        Version = "v1",
        Description = "API para el control de lectores RFID"
    });
});

// Configurar CORS para producción
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder
            .WithOrigins(
                "http://localhost:3001",
                "http://172.16.10.31:90",
                "http://172.16.10.31:92",
                "http://172.16.10.31:105"

            // Agrega aquí todos los dominios que necesites
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Configurar HttpClient
builder.Services.AddHttpClient<IProductDataService, ProductDataService>(client =>
{
    client.BaseAddress = new Uri("http://172.16.10.31/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Agregar SignalR con configuración para producción
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

// Registrar servicios como Singleton
builder.Services.AddSingleton<RFIDReaderService>();
builder.Services.AddSingleton<INotificationService, EmailNotificationService>();
builder.Services.AddSingleton<IProductDataService, ProductDataService>();

// Registrar servicios hosted
builder.Services.AddHostedService<RFIDMonitoringService>();

// Configurar servicios de email
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("SendGrid"));

// Configuración de logs
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddEventLog(settings =>
    {
        settings.SourceName = "RFID Reader Service";
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RFID Reader API v1");
    });
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Configurar CORS antes de los endpoints
app.UseCors();

app.UseRouting();
app.UseAuthorization();

// Middleware de manejo de errores global
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ProductDataException ex)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Error de datos de producto: {Message}, EPC: {EPC}, Tipo: {ErrorType}",
            ex.Message, ex.EPC, ex.ErrorType);

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new
        {
            error = ex.Message,
            errorType = ex.ErrorType.ToString(),
            epc = ex.EPC
        });
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Error no manejado en la aplicación");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Se produjo un error interno en el servidor"
        });
    }
});

// Endpoints
app.MapControllers();
app.MapHub<ReaderHub>("/readerHub");

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

app.Run();