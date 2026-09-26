using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CardEntity = Card.Domain.Entities.Card;

namespace Card.Infrastructure.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<CardEntity>
{
    public void Configure(EntityTypeBuilder<CardEntity> builder)
    {
        builder.ToTable("Cards");
        builder.HasKey(card => card.Id);
        builder.Property(card => card.Id).IsRequired().ValueGeneratedNever();
        builder.Property(card => card.ClienteId).IsRequired();
        builder.HasIndex(card => card.ClienteId);
        builder.Property(card => card.Limite).IsRequired().HasColumnType("decimal(18,2)");
        builder.Property(card => card.CriadoEm).IsRequired()
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}
