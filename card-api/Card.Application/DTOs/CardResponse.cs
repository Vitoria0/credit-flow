namespace Card.Application.DTOs;

public class CardResponse
{
    public Guid Id { get; init; }
    public Guid ClienteId { get; init; }
    public decimal Limite { get; init; }
    public DateTime CriadoEm { get; init; }
}
