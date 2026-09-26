using Credit.Application.DTOs;
using Credit.Application.Interfaces;
using Credit.Domain.Entities;
using Credit.Domain.Events;
using Credit.Domain.Exceptions;
using Credit.Domain.Interfaces;

namespace Credit.Application.Services;

public class CreditProposalService : ICreditProposalService
{
    private readonly ICreditProposalRepository _repository;
    private readonly IEventPublisher _eventPublisher;

    public CreditProposalService(ICreditProposalRepository repository, IEventPublisher eventPublisher)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
    }

    public async Task<CreditProposalResponse> CreateAsync(
        Guid clienteId, int score, CancellationToken cancellationToken)
    {
        if (await _repository.ExistsByClienteIdAsync(clienteId, cancellationToken))
        {
            throw new ProposalAlreadyExistsException(clienteId);
        }

        var proposal = new CreditProposal(clienteId, score);
        await _repository.AddAsync(proposal, cancellationToken);

        if (proposal.Status == "Aprovado")
        {
            var propostaGerada = new PropostaGeradaEvent(
                proposal.ClienteId,
                proposal.Score,
                proposal.Status,
                proposal.LimitePorCartao,
                proposal.QuantidadeCartoes);

            await _eventPublisher.PublishAsync(propostaGerada, cancellationToken);
        }

        return MapToResponse(proposal);
    }

    public async Task<CreditProposalResponse?> GetByClienteIdAsync(
        Guid clienteId, CancellationToken cancellationToken)
    {
        var proposal = await _repository.GetByClienteIdAsync(clienteId, cancellationToken);

        return proposal is null ? null : MapToResponse(proposal);
    }

    private static CreditProposalResponse MapToResponse(CreditProposal proposal)
    {
        return new CreditProposalResponse
        {
            Id = proposal.Id,
            ClienteId = proposal.ClienteId,
            Score = proposal.Score,
            Status = proposal.Status,
            LimitePorCartao = proposal.LimitePorCartao,
            QuantidadeCartoes = proposal.QuantidadeCartoes
        };
    }
}
