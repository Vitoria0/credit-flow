using System.Text.Json;
using Costumer.Domain.Events;
using Costumer.Domain.Interfaces;
using RabbitMQ.Client;

namespace Costumer.Infrastructure.MessageBroker;

public class RabbitMqEventPublisher : IEventPublisher
{
    private const string QueueName = "cliente-cadastrado";
    private readonly ConnectionFactory _connectionFactory;

    public RabbitMqEventPublisher(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PublishAsync(ClienteCadastradoEvent clienteCadastrado, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(clienteCadastrado);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: true,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
