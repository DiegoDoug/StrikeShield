using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class IntegrationConfiguration : IEntityTypeConfiguration<Integration>
{
    public void Configure(EntityTypeBuilder<Integration> builder)
    {
        builder.ToTable("Integrations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Type)
            .IsRequired();

        builder.Property(i => i.Enabled)
            .IsRequired();

        builder.Property(i => i.NotifyOnScanCompletion)
            .IsRequired();

        builder.Property(i => i.NotifyOnCriticalFinding)
            .IsRequired();

        builder.Property(i => i.WebhookUrl)
            .HasMaxLength(1000);

        builder.Property(i => i.GitHubRepository)
            .HasMaxLength(300);

        builder.Property(i => i.GitHubAccessToken)
            .HasMaxLength(500);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.HasOne(i => i.Organization)
            .WithMany(o => o.Integrations)
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.OrganizationId);
    }
}
