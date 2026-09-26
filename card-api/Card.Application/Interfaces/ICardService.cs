using Card.Application.DTOs;

namespace Card.Application.Interfaces;

public interface ICardService
{
    Task CreateCardsAsync(Guid clienteId, int score, string status, decimal limitePorCartao,
        int quantidadeCartoes, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CardResponse>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken);
}
