using Credit.Application.Services;
using Credit.Domain.Entities;
using Credit.Domain.Events;
using Credit.Domain.Exceptions;
using Credit.Domain.Interfaces;
using Moq;

namespace Credit.Tests.Application;

public class CreditProposalServiceTests
{
    private readonly Mock<ICreditProposalRepository> _repository = new();
    private readonly Mock<IEventPublisher> _publisher = new();

    private CreditProposalService CreateService() => new(_repository.Object, _publisher.Object);

    [Fact]
    public async Task CreateAsync_WithNewCliente_PersistsReturnsAndPublishesApprovedProposal()
    {
        var clienteId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        var response = await CreateService().CreateAsync(clienteId, 501, token);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(clienteId, response.ClienteId);
        Assert.Equal(501, response.Score);
        Assert.Equal("Aprovado", response.Status);
        Assert.Equal(5000m, response.LimitePorCartao);
        Assert.Equal(2, response.QuantidadeCartoes);
        _repository.Verify(repository => repository.AddAsync(It.Is<CreditProposal>(proposal =>
            proposal.Id == response.Id && proposal.ClienteId == clienteId &&
            proposal.Score == 501 && proposal.Status == "Aprovado" &&
            proposal.LimitePorCartao == 5000m && proposal.QuantidadeCartoes == 2), token), Times.Once);
        _publisher.Verify(publisher => publisher.PublishAsync(It.Is<PropostaGeradaEvent>(message =>
            message.ClienteId == clienteId && message.Score == 501 && message.Status == "Aprovado" &&
            message.LimitePorCartao == 5000m && message.QuantidadeCartoes == 2), token), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithDeniedProposal_PersistsWithoutPublishing()
    {
        var clienteId = Guid.NewGuid();

        var response = await CreateService().CreateAsync(clienteId, 100, CancellationToken.None);

        Assert.Equal("Negado", response.Status);
        Assert.Equal(0m, response.LimitePorCartao);
        Assert.Equal(0, response.QuantidadeCartoes);
        _repository.Verify(repository => repository.AddAsync(It.Is<CreditProposal>(proposal =>
            proposal.Id == response.Id && proposal.ClienteId == clienteId && proposal.Score == 100),
            CancellationToken.None), Times.Once);
        _publisher.Verify(publisher => publisher.PublishAsync(
            It.IsAny<PropostaGeradaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithAlreadyProcessedCliente_DoesNotPersistOrPublish()
    {
        var clienteId = Guid.NewGuid();
        _repository.Setup(repository => repository.ExistsByClienteIdAsync(clienteId, CancellationToken.None))
            .ReturnsAsync(true);

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            CreateService().CreateAsync(clienteId, 501, CancellationToken.None));

        var duplicate = Assert.IsType<ProposalAlreadyExistsException>(exception);
        Assert.Equal(clienteId, duplicate.ClienteId);
        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<CreditProposal>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(publisher => publisher.PublishAsync(
            It.IsAny<PropostaGeradaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PublishesOnlyAfterPersistenceCompletes()
    {
        var clienteId = Guid.NewGuid();
        var persistence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository.Setup(repository => repository.AddAsync(
            It.IsAny<CreditProposal>(), CancellationToken.None)).Returns(persistence.Task);

        var creation = CreateService().CreateAsync(clienteId, 501, CancellationToken.None);

        try
        {
            _repository.Verify(repository => repository.AddAsync(
                It.IsAny<CreditProposal>(), CancellationToken.None), Times.Once);
            _publisher.Verify(publisher => publisher.PublishAsync(
                It.IsAny<PropostaGeradaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            persistence.SetResult();
        }

        await creation;

        _publisher.Verify(publisher => publisher.PublishAsync(
            It.Is<PropostaGeradaEvent>(message => message.ClienteId == clienteId),
            CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_DoesNotPublish()
    {
        var failure = new InvalidOperationException("Falha ao persistir.");
        _repository.Setup(repository => repository.AddAsync(
            It.IsAny<CreditProposal>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreateAsync(Guid.NewGuid(), 501, CancellationToken.None));

        Assert.Same(failure, exception);
        _publisher.Verify(publisher => publisher.PublishAsync(
            It.IsAny<PropostaGeradaEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByClienteIdAsync_WhenFound_ReturnsProposal()
    {
        var proposal = new CreditProposal(Guid.NewGuid(), 101);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        _repository.Setup(repository => repository.GetByClienteIdAsync(proposal.ClienteId, token))
            .ReturnsAsync(proposal);

        var response = await CreateService().GetByClienteIdAsync(proposal.ClienteId, token);

        Assert.NotNull(response);
        Assert.Equal(proposal.Id, response.Id);
        Assert.Equal(proposal.ClienteId, response.ClienteId);
        Assert.Equal(proposal.Score, response.Score);
        Assert.Equal(proposal.Status, response.Status);
        Assert.Equal(proposal.LimitePorCartao, response.LimitePorCartao);
        Assert.Equal(proposal.QuantidadeCartoes, response.QuantidadeCartoes);
    }

    [Fact]
    public async Task GetByClienteIdAsync_WhenNotFound_ReturnsNull()
    {
        var clienteId = Guid.NewGuid();
        _repository.Setup(repository => repository.GetByClienteIdAsync(clienteId, CancellationToken.None))
            .ReturnsAsync((CreditProposal?)null);

        var response = await CreateService().GetByClienteIdAsync(clienteId, CancellationToken.None);

        Assert.Null(response);
        _repository.Verify(repository => repository.GetByClienteIdAsync(
            clienteId, CancellationToken.None), Times.Once);
    }
}
