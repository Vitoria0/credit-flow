namespace Costumer.Application.DTOs;

public class CreateCostumerRequest
{
    public string Cpf { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Nome { get; init; } = string.Empty;
}
