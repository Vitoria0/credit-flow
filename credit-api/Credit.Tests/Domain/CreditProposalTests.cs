using Credit.Domain.Entities;

namespace Credit.Tests.Domain;

public class CreditProposalTests
{
    [Theory]
    [InlineData(0, "Negado", 0, 0)]
    [InlineData(100, "Negado", 0, 0)]
    [InlineData(101, "Aprovado", 1000, 1)]
    [InlineData(500, "Aprovado", 1000, 1)]
    [InlineData(501, "Aprovado", 5000, 2)]
    [InlineData(1000, "Aprovado", 5000, 2)]
    public void Constructor_DefinesProposalFromScore(int score, string status, int limit, int cards)
    {
        var clienteId = Guid.NewGuid();

        var proposal = new CreditProposal(clienteId, score);

        Assert.NotEqual(Guid.Empty, proposal.Id);
        Assert.Equal(clienteId, proposal.ClienteId);
        Assert.Equal(score, proposal.Score);
        Assert.Equal(status, proposal.Status);
        Assert.Equal((decimal)limit, proposal.LimitePorCartao);
        Assert.Equal(cards, proposal.QuantidadeCartoes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Constructor_WithInvalidScore_Throws(int score)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CreditProposal(Guid.NewGuid(), score));

        Assert.Equal("score", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyClienteId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CreditProposal(Guid.Empty, 500));

        Assert.Equal("clienteId", exception.ParamName);
    }
}
