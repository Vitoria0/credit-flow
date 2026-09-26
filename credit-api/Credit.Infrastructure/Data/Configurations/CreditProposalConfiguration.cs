using Credit.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Credit.Infrastructure.Data.Configurations;

public class CreditProposalConfiguration : IEntityTypeConfiguration<CreditProposal>
{
    public void Configure(EntityTypeBuilder<CreditProposal> builder)
    {
        builder.ToTable("CreditProposals");

        builder.HasKey(proposal => proposal.Id);
        builder.Property(proposal => proposal.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(proposal => proposal.ClienteId)
            .IsRequired();
        builder.HasIndex(proposal => proposal.ClienteId)
            .IsUnique();

        builder.Property(proposal => proposal.Score)
            .IsRequired();

        builder.Property(proposal => proposal.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(proposal => proposal.LimitePorCartao)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(proposal => proposal.QuantidadeCartoes)
            .IsRequired();
    }
}
