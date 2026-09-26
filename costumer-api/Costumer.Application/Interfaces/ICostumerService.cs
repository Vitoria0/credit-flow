using Costumer.Application.DTOs;

namespace Costumer.Application.Interfaces;

public interface ICostumerService
{
    Task<CostumerResponse> CreateAsync(CreateCostumerRequest request, CancellationToken cancellationToken = default);
    Task<CostumerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
