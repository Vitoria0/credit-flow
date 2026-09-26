using Card.Domain.Interfaces;
using Card.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CardEntity = Card.Domain.Entities.Card;

namespace Card.Infrastructure.Repositories;

public class CardRepository : ICardRepository
{
    private readonly ApplicationDbContext _context;

    public CardRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken)
        => _context.Cards.AnyAsync(card => card.ClienteId == clienteId, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<CardEntity> cards, CancellationToken cancellationToken)
    {
        await _context.Cards.AddRangeAsync(cards, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CardEntity>> GetByClienteIdAsync(
        Guid clienteId, CancellationToken cancellationToken)
        => await _context.Cards.AsNoTracking()
            .Where(card => card.ClienteId == clienteId)
            .OrderBy(card => card.CriadoEm).ThenBy(card => card.Id)
            .ToArrayAsync(cancellationToken);
}
