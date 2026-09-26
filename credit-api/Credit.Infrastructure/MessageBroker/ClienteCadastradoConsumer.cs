using System.Text.Json;
using Credit.Application.Interfaces;
using Credit.Domain.Events;
using Credit.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Credit.Infrastructure.MessageBroker;

public class ClienteCadastradoConsumer : BackgroundService
{
    private const string QueueName = "cliente-cadastrado";
    private const string RetryQueueName = "cliente-cadastrado.retry";
    private const string DeadLetterQueueName = "cliente-cadastrado.dlq";
    private const string RetryHeader = "x-retry-count";
    private const int MaxRetries = 3;
    private readonly ConnectionFactory _connectionFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ClienteCadastradoConsumer> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ClienteCadastradoConsumer(
        ConnectionFactory connectionFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<ClienteCadastradoConsumer> logger,
        IHostApplicationLifetime lifetime)
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
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true), stoppingToken);

            await channel.QueueDeclareAsync(
                queue: QueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(
                queue: RetryQueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = 5000,
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = QueueName
                }, cancellationToken: stoppingToken);
            await channel.QueueDeclareAsync(
                queue: DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: null, cancellationToken: stoppingToken);
            await channel.BasicQosAsync(0, 1, false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                Guid? clienteId = null;
                var retryCount = ReadRetryCount(delivery.BasicProperties);
                try
                {
                    var message = JsonSerializer.Deserialize<ClienteCadastradoEvent>(delivery.Body.Span)
                        ?? throw new JsonException("Mensagem ClienteCadastrado vazia.");
                    clienteId = message.ClienteId;
                    _logger.LogInformation("Processando ClienteId {ClienteId}, tentativa {Tentativa}.",
                        clienteId, retryCount + 1);

                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var scoreGenerator = scope.ServiceProvider.GetRequiredService<IScoreGenerator>();
                    var service = scope.ServiceProvider.GetRequiredService<ICreditProposalService>();

                    await service.CreateAsync(message.ClienteId, scoreGenerator.Generate(), stoppingToken);
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                }
                catch (ProposalAlreadyExistsException exception)
                {
                    _logger.LogInformation(
                        "ClienteId {ClienteId}: proposta já processada; mensagem ignorada por idempotência.",
                        exception.ClienteId);
                    await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // O fechamento do canal libera mensagens ainda não confirmadas.
                }
                catch (Exception exception)
                {
                    var destination = retryCount < MaxRetries ? RetryQueueName : DeadLetterQueueName;
                    _logger.LogError(exception,
                        "Falha para ClienteId {ClienteId}, tentativa {Tentativa}. Destino: {Destino}.",
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

                        await publishChannel.BasicPublishAsync(
                            exchange: string.Empty, routingKey: destination, mandatory: true,
                            basicProperties: properties, body: delivery.Body,
                            cancellationToken: stoppingToken);
                        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                        _logger.LogInformation("ClienteId {ClienteId} enviado para {Destino}, x-retry-count {RetryCount}.",
                            clienteId, destination, properties.Headers[RetryHeader]);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Sem ACK: a mensagem permanece sob responsabilidade do broker.
                    }
                    catch (Exception publishException)
                    {
                        _logger.LogError(publishException,
                            "Falha ao encaminhar ClienteId {ClienteId} para {Destino}. Encerrando sem ACK.",
                            clienteId, destination);
                        _lifetime.StopApplication();
                    }
                }
            };

            await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer,
                cancellationToken: stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento solicitado pela aplicação.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Falha no consumer ClienteCadastrado.");
            throw;
        }
    }

    private static int ReadRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(RetryHeader, out var value))
        {
            return 0;
        }

        // Headers inválidos seguem para a DLQ em caso de falha, sem reiniciar a contagem.
        return value switch
        {
            int count when count >= 0 => Math.Min(count, MaxRetries),
            long count when count >= 0 => (int)Math.Min(count, MaxRetries),
            _ => MaxRetries
        };
    }
}
