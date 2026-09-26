using Costumer.Application.DTOs;
using Costumer.Application.Interfaces;
using Costumer.Domain.Interfaces;
using Costumer.Domain.Events;
using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Application.Services;

public class CostumerService : ICostumerService
{
    private readonly ICostumerRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public CostumerService(ICostumerRepository repository, IEventPublisher eventPublisher)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<CostumerResponse> CreateAsync(CreateCostumerRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.ExistsByCpfAsync(request.Cpf, cancellationToken))
        {
            throw new InvalidOperationException("CPF já cadastrado.");
        }

        var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;

        if (await _repository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
        {
            throw new InvalidOperationException("Email já cadastrado.");
        }

        var costumer = new CostumerEntity(request.Cpf, normalizedEmail, request.Nome);
        await _repository.AddAsync(costumer, cancellationToken);
        await _eventPublisher.PublishAsync(new ClienteCadastradoEvent(costumer.Id), cancellationToken);

        return MapToResponse(costumer);
    }

    public async Task<CostumerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var costumer = await _repository.GetByIdAsync(id, cancellationToken);

        return costumer is null ? null : MapToResponse(costumer);
    }

    private static CostumerResponse MapToResponse(CostumerEntity costumer)
    {
        return new CostumerResponse
        {
            Id = costumer.Id,
            Cpf = costumer.Cpf,
            Email = costumer.Email,
            Nome = costumer.Nome
        };
    }
}
