namespace Card.Domain.Events;

public record PropostaGeradaEvent(
    Guid ClienteId,
    int Score,
    string Status,
    decimal LimitePorCartao,
    int QuantidadeCartoes);
