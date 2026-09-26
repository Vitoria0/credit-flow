using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Domain.Interfaces;

public interface ICostumerRepository
{
    Task<bool> ExistsByCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(CostumerEntity costumer, CancellationToken cancellationToken = default);
    Task<CostumerEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
