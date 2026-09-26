using Credit.Domain.Events;

namespace Credit.Domain.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(PropostaGeradaEvent propostaGerada, CancellationToken cancellationToken);
}
