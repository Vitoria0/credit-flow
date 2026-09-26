using Costumer.Domain.Events;

namespace Costumer.Domain.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync(ClienteCadastradoEvent clienteCadastrado, CancellationToken cancellationToken = default);
}
