using System.Text.Json;
using CreditosApp.Data;
using CreditosApp.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Services;

public sealed class NotificacionConsumerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificacionConsumerService> _logger;
    private readonly Uri _uri;
    private readonly string _queueName;

    public NotificacionConsumerService(IServiceProvider services, IConfiguration config, ILogger<NotificacionConsumerService> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
        _uri = new Uri(_config["RabbitMq__ConnectionString"] ?? "amqp://guest:guest@localhost:5672");
        _queueName = _config["RabbitMq__QueueName"] ?? "solicitudes.notificaciones";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(_config["RabbitMq__ConsumerEnabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Consumidor de notificaciones deshabilitado (RabbitMq__ConsumerEnabled=false). Exit inmediato.");
            return;
        }

        var factory = new ConnectionFactory { Uri = _uri };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    try
                    {
                        await ProcesarMensajeAsync(channel, ea);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error procesando el mensaje RabbitMQ (deliveryTag {DeliveryTag}). Ejecutando Nack sin requeue.", ea.DeliveryTag);
                        try
                        {
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
                        }
                        catch (Exception nackEx)
                        {
                            _logger.LogError(nackEx, "No se pudo ejecutar el Nack del mensaje {DeliveryTag}", ea.DeliveryTag);
                        }
                    }
                };

                await channel.BasicConsumeAsync(
                    queue: _queueName,
                    autoAck: false,
                    consumerTag: string.Empty,
                    noLocal: false,
                    exclusive: false,
                    arguments: null,
                    consumer: consumer);

                _logger.LogInformation("Consumidor de notificaciones escuchando en {Cola}", _queueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión del consumidor de notificaciones. Reintentando en 10 segundos.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcesarMensajeAsync(IChannel channel, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var evento = JsonSerializer.Deserialize<SolicitudRegistrada>(body);

        if (evento is null)
        {
            _logger.LogWarning("Mensaje con formato inválido (deliveryTag {DeliveryTag}). Nack sin requeue.", ea.DeliveryTag);
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        using (var scope = _services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existe = await db.Notificaciones.AnyAsync(n => n.MessageId == evento.MessageId);

            if (existe)
            {
                _logger.LogInformation("Mensaje duplicado {MessageId}. ACK inmediato sin insertar.", evento.MessageId);
                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                return;
            }

            db.Notificaciones.Add(new Notificacion
            {
                MessageId = evento.MessageId,
                SolicitudId = evento.SolicitudId,
                UsuarioId = evento.UsuarioId,
                Texto = "Recibimos tu solicitud de crédito y está pendiente de evaluación",
                FechaProcesamientoUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
        _logger.LogInformation("Notificación {MessageId} procesada y guardada.", evento.MessageId);
    }
}