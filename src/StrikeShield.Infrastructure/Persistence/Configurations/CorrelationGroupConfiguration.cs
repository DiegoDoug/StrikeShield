using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class CorrelationGroupConfiguration : IEntityTypeConfiguration<CorrelationGroup>
{
    public void Configure(EntityTypeBuilder<CorrelationGroup> builder)
    {
        builder.ToTable("CorrelationGroups");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CreatedAt)
            .IsRequired();
    }
}
