using System.Text.Json;
using Card.Application.Interfaces;
using Card.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Card.Infrastructure.MessageBroker;

public class PropostaGeradaConsumer : BackgroundService
{
    private const string QueueName = "proposta-gerada";
    private const string RetryQueueName = "card.proposta-gerada.retry";
    private const string DeadLetterQueueName = "card.proposta-gerada.dlq";
    private const string RetryHeader = "x-retry-count";
    private const int MaxRetries = 3;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly ConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PropostaGeradaConsumer> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public PropostaGeradaConsumer(ConnectionFactory connectionFactory, IServiceScopeFactory scopeFactory,
        ILogger<PropostaGeradaConsumer> logger, IHostApplicationLifetime lifetime)
    {
        _connectionFactory = connectionFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await using var publishChannel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true), stoppingToken);

            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(RetryQueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = 5000,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = QueueName
                }, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: stoppingToken);
            await channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                Guid? clienteId = null;
                var retryCount = ReadRetryCount(delivery.BasicProperties);
                try
                {
                    var message = JsonSerializer.Deserialize<PropostaGeradaEvent>(delivery.Body.Span, JsonOptions)
                        ?? throw new JsonException("Mensagem PropostaGerada vazia.");
                    clienteId = message.ClienteId;
                    _logger.LogInformation("ClienteId {ClienteId}: início do processamento, tentativa {Tentativa}.",
                        clienteId, retryCount + 1);

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<ICardService>();
                    await service.CreateCardsAsync(message.ClienteId, message.Score, message.Status,
                        message.LimitePorCartao, message.QuantidadeCartoes, stoppingToken);
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // O fechamento do canal libera a mensagem ainda não confirmada.
                }
                catch (Exception exception)
                {
                    var destination = retryCount < MaxRetries ? RetryQueueName : DeadLetterQueueName;
                    _logger.LogError(exception,
                        "ClienteId {ClienteId}: falha na tentativa {Tentativa}; destino {Destino}.",
                        clienteId, retryCount + 1, destination);
                    try
                    {
                        var properties = new BasicProperties(delivery.BasicProperties)
                        {
                            Persistent = true,
                            Expiration = null,
                            Headers = delivery.BasicProperties.Headers is null
                                ? new Dictionary<string, object?>()
                                : new Dictionary<string, object?>(delivery.BasicProperties.Headers)
                        };
                        properties.Headers[RetryHeader] = retryCount < MaxRetries ? retryCount + 1 : retryCount;

                        await publishChannel.BasicPublishAsync(string.Empty, destination, mandatory: true,
                            basicProperties: properties, body: delivery.Body, cancellationToken: stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        _logger.LogInformation("ClienteId {ClienteId}: enviado para {Destino}, retry {RetryCount}.",
                            clienteId, destination, properties.Headers[RetryHeader]);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Sem ACK durante o encerramento.
                    }
                    catch (Exception forwardingException)
                    {
                        _logger.LogError(forwardingException,
                            "ClienteId {ClienteId}: falha ao encaminhar para {Destino}; encerrando sem ACK.",
                            clienteId, destination);
                        _lifetime.StopApplication();
                    }
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer,
                cancellationToken: stoppingToken);
            // Mantém o hosted service ativo; o atraso de retry é exclusivamente o TTL do broker.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha no consumer PropostaGerada.");
            throw;
        }
    }

    private static int ReadRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(RetryHeader, out var value))
            return 0;

        // Um header inválido não reinicia o ciclo de retry.
        return value switch
        {
            int count when count >= 0 => Math.Min(count, MaxRetries),
            long count when count >= 0 => (int)Math.Min(count, MaxRetries),
            _ => MaxRetries
        };
    }
}
