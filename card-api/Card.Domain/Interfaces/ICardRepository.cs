using CardEntity = Card.Domain.Entities.Card;

namespace Card.Domain.Interfaces;

public interface ICardRepository
{
    Task<bool> ExistsByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);
    Task AddRangeAsync(IEnumerable<CardEntity> cards, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CardEntity>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);
}
