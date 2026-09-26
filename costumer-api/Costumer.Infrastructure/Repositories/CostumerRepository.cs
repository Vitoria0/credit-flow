using Costumer.Domain.Interfaces;
using Costumer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Infrastructure.Repositories;

public class CostumerRepository : ICostumerRepository
{
    private readonly ApplicationDbContext _context;

    public CostumerRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        return _context.Costumers.AnyAsync(costumer => costumer.Cpf == cpf, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _context.Costumers.AnyAsync(costumer => costumer.Email == email, cancellationToken);
    }

    public async Task AddAsync(CostumerEntity costumer, CancellationToken cancellationToken = default)
    {
        await _context.Costumers.AddAsync(costumer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<CostumerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Costumers
            .AsNoTracking()
            .FirstOrDefaultAsync(costumer => costumer.Id == id, cancellationToken);
    }
}
