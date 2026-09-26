using Costumer.Application.DTOs;
using Costumer.Application.Services;
using Costumer.Domain.Events;
using Costumer.Domain.Interfaces;
using Moq;
using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Tests.Application;

public class CostumerServiceTests
{
    private readonly Mock<ICostumerRepository> _repository = new();
    private readonly Mock<IEventPublisher> _publisher = new();

    private CostumerService CreateService() => new(_repository.Object, _publisher.Object);

    private static CreateCostumerRequest CreateRequest() => new()
    {
        Cpf = "12345678901",
        Email = "maria@email.com",
        Nome = "Maria"
    };

    [Fact]
    public async Task CreateAsync_WithValidData_PersistsAndReturnsCostumer()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var request = CreateRequest();

        var response = await CreateService().CreateAsync(request, token);

        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal(request.Cpf, response.Cpf);
        Assert.Equal(request.Email, response.Email);
        Assert.Equal(request.Nome, response.Nome);
        _repository.Verify(repository => repository.AddAsync(
            It.Is<CostumerEntity>(costumer =>
                costumer.Id == response.Id && costumer.Cpf == request.Cpf &&
                costumer.Email == request.Email && costumer.Nome == request.Nome), token), Times.Once);
    }

    [Theory]
    [InlineData(true, false, "CPF já cadastrado.")]
    [InlineData(false, true, "Email já cadastrado.")]
    public async Task CreateAsync_WithDuplicate_DoesNotPersistOrPublish(
        bool cpfExists, bool emailExists, string expectedMessage)
    {
        var request = CreateRequest();
        _repository.Setup(repository => repository.ExistsByCpfAsync(request.Cpf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cpfExists);
        _repository.Setup(repository => repository.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emailExists);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreateAsync(request));

        Assert.Equal(expectedMessage, exception.Message);
        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<CostumerEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(publisher => publisher.PublishAsync(
            It.IsAny<ClienteCadastradoEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PublishesEventOnlyAfterPersistenceCompletes()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var persistence = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _repository.Setup(repository => repository.AddAsync(It.IsAny<CostumerEntity>(), token))
            .Returns(persistence.Task);

        var creation = CreateService().CreateAsync(CreateRequest(), token);

        try
        {
            _repository.Verify(repository => repository.AddAsync(It.IsAny<CostumerEntity>(), token), Times.Once);
            _publisher.Verify(publisher => publisher.PublishAsync(
                It.IsAny<ClienteCadastradoEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            persistence.SetResult();
        }

        var response = await creation;

        _publisher.Verify(publisher => publisher.PublishAsync(
            It.Is<ClienteCadastradoEvent>(message => message.ClienteId == response.Id), token), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenPersistenceFails_DoesNotPublish()
    {
        var failure = new InvalidOperationException("Falha ao persistir.");
        _repository.Setup(repository => repository.AddAsync(
                It.IsAny<CostumerEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().CreateAsync(CreateRequest()));

        Assert.Same(failure, exception);
        _publisher.Verify(publisher => publisher.PublishAsync(
            It.IsAny<ClienteCadastradoEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsCostumer()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var costumer = new CostumerEntity("12345678901", "maria@email.com", "Maria");
        _repository.Setup(repository => repository.GetByIdAsync(costumer.Id, token))
            .ReturnsAsync(costumer);

        var response = await CreateService().GetByIdAsync(costumer.Id, token);

        Assert.NotNull(response);
        Assert.Equal(costumer.Id, response.Id);
        Assert.Equal(costumer.Cpf, response.Cpf);
        Assert.Equal(costumer.Email, response.Email);
        Assert.Equal(costumer.Nome, response.Nome);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        var id = Guid.NewGuid();
        _repository.Setup(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CostumerEntity?)null);

        var response = await CreateService().GetByIdAsync(id);

        Assert.Null(response);
        _repository.Verify(repository => repository.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
