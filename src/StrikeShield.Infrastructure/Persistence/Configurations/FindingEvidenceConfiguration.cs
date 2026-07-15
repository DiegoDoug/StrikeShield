using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class FindingEvidenceConfiguration : IEntityTypeConfiguration<FindingEvidence>
{
    public void Configure(EntityTypeBuilder<FindingEvidence> builder)
    {
        builder.ToTable("FindingEvidence");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(e => e.CreatedAt)
            .IsRequired();
    }
}
