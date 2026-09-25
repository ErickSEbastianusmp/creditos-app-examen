using System.Text.Json;
using CreditosApp.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CreditosApp.Services;

public interface IRabbitMQService
{
    Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistrada evento);
}

public class RabbitMQPublisher : IRabbitMQService
{
    private readonly IConfiguration _config;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private readonly Uri _uri;
    private readonly string _queueName;

    public RabbitMQPublisher(IConfiguration config, ILogger<RabbitMQPublisher> logger)
    {
        _config = config;
        _logger = logger;
        _uri = new Uri(_config["RabbitMq__ConnectionString"] ?? "amqp://guest:guest@localhost:5672");
        _queueName = _config["RabbitMq__QueueName"] ?? "solicitudes.notificaciones";
    }

    public async Task<bool> PublicarSolicitudRegistradaAsync(SolicitudRegistrada evento)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = _uri };

            await using var connection = await factory.CreateConnectionAsync();

            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true,
                outstandingPublisherConfirmationsRateLimiter: null,
                consumerDispatchConcurrency: null);

            await using var channel = await connection.CreateChannelAsync(channelOptions);

            await channel.QueueDeclareAsync(
                queue: _queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var seqNo = await channel.GetNextPublishSequenceNumberAsync();

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            AsyncEventHandler<BasicAckEventArgs>? ackHandler = null;
            AsyncEventHandler<BasicNackEventArgs>? nackHandler = null;

            ackHandler = (_, ea) =>
            {
                if (ea.DeliveryTag >= seqNo)
                {
                    tcs.TrySetResult(true);
                }
                return Task.CompletedTask;
            };

            nackHandler = (_, ea) =>
            {
                if (ea.DeliveryTag >= seqNo)
                {
                    tcs.TrySetException(new InvalidOperationException($"RabbitMQ rechazó el mensaje (deliveryTag {ea.DeliveryTag})."));
                }
                return Task.CompletedTask;
            };

            channel.BasicAcksAsync += ackHandler;
            channel.BasicNacksAsync += nackHandler;

            try
            {
                var props = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _queueName,
                    mandatory: false,
                    basicProperties: props,
                    body: JsonSerializer.SerializeToUtf8Bytes(evento));

                await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));

                _logger.LogInformation("SolicitudRegistrada {MessageId} publicada y confirmada en la cola {Cola}", evento.MessageId, _queueName);
                return true;
            }
            finally
            {
                channel.BasicAcksAsync -= ackHandler;
                channel.BasicNacksAsync -= nackHandler;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo publicar la notificación de la solicitud {SolicitudId} en RabbitMQ", evento.SolicitudId);
            return false;
        }
    }
}