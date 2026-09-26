using Credit.Application.DTOs;

namespace Credit.Application.Interfaces;

public interface ICreditProposalService
{
    Task<CreditProposalResponse> CreateAsync(
        Guid clienteId, int score, CancellationToken cancellationToken);

    Task<CreditProposalResponse?> GetByClienteIdAsync(
        Guid clienteId, CancellationToken cancellationToken);
}
