using Credit.Domain.Entities;
using Credit.Domain.Interfaces;
using Credit.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Credit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Credit.Infrastructure.Repositories;

public class CreditProposalRepository : ICreditProposalRepository
{
    private readonly ApplicationDbContext _context;

    public CreditProposalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken)
    {
        return _context.CreditProposals.AnyAsync(
            proposal => proposal.ClienteId == clienteId, cancellationToken);
    }

    public async Task AddAsync(CreditProposal proposal, CancellationToken cancellationToken)
    {
        await _context.CreditProposals.AddAsync(proposal, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqlException sqlException &&
            sqlException.Errors.Cast<SqlError>().Any(error =>
                error.Number is 2601 or 2627 &&
                error.Message.Contains("'IX_CreditProposals_ClienteId'", StringComparison.Ordinal) &&
                error.Message.Contains("'dbo.CreditProposals'", StringComparison.Ordinal)))
        {
            throw new ProposalAlreadyExistsException(proposal.ClienteId, exception);
        }
    }

    public Task<CreditProposal?> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken)
    {
        return _context.CreditProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(proposal => proposal.ClienteId == clienteId, cancellationToken);
    }
}
