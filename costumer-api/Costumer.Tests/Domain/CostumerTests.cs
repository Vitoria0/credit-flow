using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Tests.Domain;

public class CostumerTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesCostumer()
    {
        var costumer = new CostumerEntity("12345678901", "maria@email.com", "Maria");

        Assert.NotEqual(Guid.Empty, costumer.Id);
        Assert.Equal("12345678901", costumer.Cpf);
        Assert.Equal("maria@email.com", costumer.Email);
        Assert.Equal("Maria", costumer.Nome);
    }

    [Theory]
    [InlineData("1234567890")]
    [InlineData("123456789012")]
    [InlineData("")]
    [InlineData("123.456.789-01")]
    public void Constructor_WithInvalidCpfLength_Throws(string cpf)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CostumerEntity(cpf, "maria@email.com", "Maria"));

        Assert.Equal("cpf", exception.ParamName);
    }

    [Theory]
    [InlineData("1234567890a")]
    [InlineData("123456789-1")]
    public void Constructor_WithNonNumericCpf_Throws(string cpf)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CostumerEntity(cpf, "maria@email.com", "Maria"));

        Assert.Equal("cpf", exception.ParamName);
    }

    [Theory]
    [InlineData("email-invalido")]
    [InlineData("maria@@email.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidEmail_Throws(string email)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CostumerEntity("12345678901", email, "Maria"));

        Assert.Equal("email", exception.ParamName);
    }

    [Fact]
    public void Constructor_NormalizesEmail()
    {
        var costumer = new CostumerEntity("12345678901", "  MARIA@EMAIL.COM  ", "Maria");

        Assert.Equal("maria@email.com", costumer.Email);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var costumer = new CostumerEntity("12345678901", "maria@email.com", "  Maria Silva  ");
        Assert.Equal("Maria Silva", costumer.Nome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyName_Throws(string nome)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new CostumerEntity("12345678901", "maria@email.com", nome));

        Assert.Equal("nome", exception.ParamName);
    }
}
