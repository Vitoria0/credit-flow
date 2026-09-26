namespace Card.Domain.Entities;

public class Card
{
    public Guid Id { get; private set; }
    public Guid ClienteId { get; private set; }
    public decimal Limite { get; private set; }
    public DateTime CriadoEm { get; private set; }

    protected Card()
    {
    }

    public Card(Guid clienteId, decimal limite)
    {
        if (clienteId == Guid.Empty)
            throw new ArgumentException("O cliente é obrigatório.", nameof(clienteId));

        if (limite <= 0)
            throw new ArgumentOutOfRangeException(nameof(limite), "O limite deve ser maior que zero.");

        Id = Guid.NewGuid();
        ClienteId = clienteId;
        Limite = limite;
        CriadoEm = DateTime.UtcNow;
    }
}
