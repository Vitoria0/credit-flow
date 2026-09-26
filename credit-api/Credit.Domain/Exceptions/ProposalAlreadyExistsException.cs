namespace Credit.Domain.Exceptions;

public class ProposalAlreadyExistsException : InvalidOperationException
{
    public ProposalAlreadyExistsException(Guid clienteId, Exception? innerException = null)
        : base("Proposta já existe para este cliente.", innerException)
    {
        ClienteId = clienteId;
    }

    public Guid ClienteId { get; }
}
