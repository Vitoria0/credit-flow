namespace Credit.Domain.Entities;

public class CreditProposal
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public int Score { get; private set; }
    public string Status { get; private set; } = null!;
    public decimal LimitePorCartao { get; private set; }
    public int QuantidadeCartoes { get; private set; }

    protected CreditProposal()
    {
    }

    public CreditProposal(Guid clienteId, int score)
    {
        if (clienteId == Guid.Empty)
        {
            throw new ArgumentException("O cliente é obrigatório.", nameof(clienteId));
        }

        if (score < 0 || score > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "O score deve estar entre 0 e 1000.");
        }

        Id = Guid.NewGuid();
        ClienteId = clienteId;
        Score = score;

        if (score <= 100)
        {
            Status = "Negado";
            LimitePorCartao = 0m;
            QuantidadeCartoes = 0;
        }
        else if (score <= 500)
        {
            Status = "Aprovado";
            LimitePorCartao = 1000m;
            QuantidadeCartoes = 1;
        }
        else
        {
            Status = "Aprovado";
            LimitePorCartao = 5000m;
            QuantidadeCartoes = 2;
        }
    }
}
