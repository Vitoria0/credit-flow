using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CostumerEntity = Costumer.Domain.Entities.Costumer;

namespace Costumer.Infrastructure.Data.Configurations;

public class CostumerConfiguration : IEntityTypeConfiguration<CostumerEntity>
{
    public void Configure(EntityTypeBuilder<CostumerEntity> builder)
    {
        builder.ToTable("Costumers");

        builder.HasKey(costumer => costumer.Id);
        builder.Property(costumer => costumer.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(costumer => costumer.Cpf)
            .IsRequired()
            .HasMaxLength(11)
            .IsUnicode(false);
        builder.HasIndex(costumer => costumer.Cpf).IsUnique();

        builder.Property(costumer => costumer.Email)
            .IsRequired()
            .HasMaxLength(255);
        builder.HasIndex(costumer => costumer.Email).IsUnique();

        builder.Property(costumer => costumer.Nome)
            .IsRequired()
            .HasMaxLength(100);
    }
}
