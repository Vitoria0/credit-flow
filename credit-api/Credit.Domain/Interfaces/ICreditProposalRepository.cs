using Credit.Domain.Entities;

namespace Credit.Domain.Interfaces;

public interface ICreditProposalRepository
{
    Task<bool> ExistsByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);
    Task AddAsync(CreditProposal proposal, CancellationToken cancellationToken);
    Task<CreditProposal?> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);
}
