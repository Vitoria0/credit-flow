using System.Net.Mail;

namespace Costumer.Domain.Entities;

public class Costumer
{
    public Guid Id { get; private set; }
    public string Cpf { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string Nome { get; private set; } = null!;

    protected Costumer()
    {
    }

    public Costumer(string cpf, string email, string nome)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11 ||
            cpf.Any(character => character < '0' || character > '9'))
        {
            throw new ArgumentException("O CPF deve conter exatamente 11 dígitos, sem máscara.", nameof(cpf));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("O e-mail é obrigatório.", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!MailAddress.TryCreate(normalizedEmail, out var address) ||
            address.Address != normalizedEmail)
        {
            throw new ArgumentException("O e-mail deve possuir um formato válido.", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ArgumentException("O nome é obrigatório.", nameof(nome));
        }

        Id = Guid.NewGuid();
        Cpf = cpf;
        Email = normalizedEmail;
        Nome = nome.Trim();
    }
}
