namespace Credit.Application.DTOs;

public class CreditProposalResponse
{
    public Guid Id { get; init; }
    public Guid ClienteId { get; init; }
    public int Score { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal LimitePorCartao { get; init; }
    public int QuantidadeCartoes { get; init; }
}
