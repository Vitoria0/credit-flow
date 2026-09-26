using Card.Application.DTOs;
using Card.Application.Interfaces;
using Card.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using CardEntity = Card.Domain.Entities.Card;

namespace Card.Application.Services;

public class CardService : ICardService
{
    private readonly ICardRepository _repository;
    private readonly ILogger<CardService> _logger;

    public CardService(ICardRepository repository, ILogger<CardService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task CreateCardsAsync(Guid clienteId, int score, string status,
        decimal limitePorCartao, int quantidadeCartoes, CancellationToken cancellationToken)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("O cliente é obrigatório.", nameof(clienteId));

        if (status != "Aprovado")
        {
            _logger.LogInformation("ClienteId {ClienteId}: proposta não aprovada, evento ignorado.", clienteId);
            return;
        }

        if (score < 0 || score > 1000)
            throw new ArgumentOutOfRangeException(nameof(score), "O score deve estar entre 0 e 1000.");

        if (score <= 100)
            throw new ArgumentException("Proposta aprovada incompatível com o score.", nameof(score));

        var expectedQuantity = score <= 500 ? 1 : 2;
        var expectedLimit = score <= 500 ? 1000m : 5000m;

        if (quantidadeCartoes != expectedQuantity)
            throw new ArgumentException("Quantidade de cartões incompatível com o score.", nameof(quantidadeCartoes));

        if (limitePorCartao != expectedLimit)
            throw new ArgumentException("Limite por cartão incompatível com o score.", nameof(limitePorCartao));

        if (await _repository.ExistsByClienteIdAsync(clienteId, cancellationToken))
        {
            _logger.LogInformation("ClienteId {ClienteId}: evento ignorado por idempotência.", clienteId);
            return;
        }

        var cards = Enumerable.Range(0, quantidadeCartoes)
            .Select(_ => new CardEntity(clienteId, limitePorCartao))
            .ToArray();

        await _repository.AddRangeAsync(cards, cancellationToken);
        _logger.LogInformation("ClienteId {ClienteId}: {Quantidade} cartões criados.", clienteId, cards.Length);
    }

    public async Task<IReadOnlyCollection<CardResponse>> GetByClienteIdAsync(
        Guid clienteId, CancellationToken cancellationToken)
    {
        var cards = await _repository.GetByClienteIdAsync(clienteId, cancellationToken);
        return cards.Select(card => new CardResponse
        {
            Id = card.Id,
            ClienteId = card.ClienteId,
            Limite = card.Limite,
            CriadoEm = card.CriadoEm
        }).ToArray();
    }
}
