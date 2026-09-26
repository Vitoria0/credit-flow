using Card.Application.Services;
using Card.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CardEntity = Card.Domain.Entities.Card;

namespace Card.Tests.Application;

public class CardServiceTests
{
    private readonly Mock<ICardRepository> _repository = new();
    private CardService CreateService() => new(_repository.Object, NullLogger<CardService>.Instance);

    [Theory]
    [InlineData(101, 1000, 1)]
    [InlineData(500, 1000, 1)]
    [InlineData(501, 5000, 2)]
    [InlineData(1000, 5000, 2)]
    public async Task CreateCardsAsync_WithApprovedProposal_PersistsExpectedCards(int score, int limit, int quantity)
    {
        var clienteId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        CardEntity[]? saved = null;
        _repository.Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<CardEntity>>(), token))
            .Callback<IEnumerable<CardEntity>, CancellationToken>((cards, _) => saved = cards.ToArray())
            .Returns(Task.CompletedTask);

        await CreateService().CreateCardsAsync(clienteId, score, "Aprovado", limit, quantity, token);

        Assert.NotNull(saved);
        Assert.Equal(quantity, saved.Length);
        Assert.Equal(quantity, saved.Select(card => card.Id).Distinct().Count());
        Assert.All(saved, card =>
        {
            Assert.Equal(clienteId, card.ClienteId);
            Assert.Equal((decimal)limit, card.Limite);
        });
        Assert.True(saved.Sum(card => card.Limite) <= 10000m);
        _repository.Verify(repository => repository.ExistsByClienteIdAsync(clienteId, token), Times.Once);
        _repository.Verify(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<CardEntity>>(), token), Times.Once);
    }

    [Fact]
    public async Task CreateCardsAsync_WithDeniedProposal_DoesNotCreateCards()
    {
        await CreateService().CreateCardsAsync(Guid.NewGuid(), 100, "Negado", 0m, 0, CancellationToken.None);
        _repository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<CardEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateCardsAsync_WithExistingCards_ReturnsWithoutCreating()
    {
        var clienteId = Guid.NewGuid();
        _repository.Setup(repository => repository.ExistsByClienteIdAsync(clienteId, CancellationToken.None))
            .ReturnsAsync(true);

        await CreateService().CreateCardsAsync(clienteId, 700, "Aprovado", 5000m, 2, CancellationToken.None);

        _repository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<CardEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateCardsAsync_WithEmptyClienteId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().CreateCardsAsync(
            Guid.Empty, 500, "Aprovado", 1000m, 1, CancellationToken.None));
        _repository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<CardEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 1000, 1)]
    [InlineData(100, 1000, 1)]
    [InlineData(-1, 1000, 1)]
    [InlineData(1001, 5000, 2)]
    [InlineData(101, 1000, 2)]
    [InlineData(501, 5000, 1)]
    [InlineData(501, 5000, 3)]
    [InlineData(101, 5000, 1)]
    [InlineData(501, 1000, 2)]
    public async Task CreateCardsAsync_WithInconsistentData_ThrowsWithoutPersisting(int score, int limit, int quantity)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateService().CreateCardsAsync(
            Guid.NewGuid(), score, "Aprovado", limit, quantity, CancellationToken.None));

        _repository.Verify(repository => repository.AddRangeAsync(
            It.IsAny<IEnumerable<CardEntity>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByClienteIdAsync_WhenFound_MapsCards()
    {
        var clienteId = Guid.NewGuid();
        var cards = new[] { new CardEntity(clienteId, 5000m), new CardEntity(clienteId, 5000m) };
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _repository.Setup(repository => repository.GetByClienteIdAsync(clienteId, token)).ReturnsAsync(cards);

        var response = await CreateService().GetByClienteIdAsync(clienteId, token);

        Assert.Equal(2, response.Count);
        foreach (var card in cards)
        {
            var dto = Assert.Single(response, item => item.Id == card.Id);
            Assert.Equal(card.ClienteId, dto.ClienteId);
            Assert.Equal(card.Limite, dto.Limite);
            Assert.Equal(card.CriadoEm, dto.CriadoEm);
        }
    }

    [Fact]
    public async Task GetByClienteIdAsync_WhenNotFound_ReturnsEmptyCollection()
    {
        var clienteId = Guid.NewGuid();
        _repository.Setup(repository => repository.GetByClienteIdAsync(clienteId, CancellationToken.None))
            .ReturnsAsync(Array.Empty<CardEntity>());

        var response = await CreateService().GetByClienteIdAsync(clienteId, CancellationToken.None);

        Assert.Empty(response);
    }
}
