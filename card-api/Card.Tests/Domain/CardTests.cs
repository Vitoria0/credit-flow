using CardEntity = Card.Domain.Entities.Card;

namespace Card.Tests.Domain;

public class CardTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesCard()
    {
        var clienteId = Guid.NewGuid();
        var before = DateTime.UtcNow;
        var card = new CardEntity(clienteId, 1000m);

        Assert.NotEqual(Guid.Empty, card.Id);
        Assert.Equal(clienteId, card.ClienteId);
        Assert.Equal(1000m, card.Limite);
        Assert.Equal(DateTimeKind.Utc, card.CriadoEm.Kind);
        Assert.InRange(card.CriadoEm, before, DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_WithEmptyClienteId_Throws()
    {
        var exception = Assert.Throws<ArgumentException>(() => new CardEntity(Guid.Empty, 1000m));
        Assert.Equal("clienteId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveLimit_Throws(int limit)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new CardEntity(Guid.NewGuid(), limit));
        Assert.Equal("limite", exception.ParamName);
    }
}
