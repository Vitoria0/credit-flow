using System.Text.Json;
using Credit.Domain.Events;
using Credit.Domain.Interfaces;
using RabbitMQ.Client;

namespace Credit.Infrastructure.MessageBroker;

public class RabbitMqEventPublisher : IEventPublisher
{
    private const string QueueName = "proposta-gerada";
    private readonly ConnectionFactory _connectionFactory;

    public RabbitMqEventPublisher(ConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task PublishAsync(PropostaGeradaEvent propostaGerada, CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(propostaGerada);
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
