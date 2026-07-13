using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class EngagementConfiguration : IEntityTypeConfiguration<Engagement>
{
    public void Configure(EntityTypeBuilder<Engagement> builder)
    {
        builder.ToTable("Engagements");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.RulesOfEngagement)
            .HasMaxLength(4000);

        builder.Property(e => e.AuthorizationEvidenceUri)
            .HasMaxLength(1000);

        builder.Property(e => e.ApprovedBy)
            .HasMaxLength(200);

        builder.Property(e => e.ScopeStart)
            .IsRequired();

        builder.Property(e => e.ScopeEnd)
            .IsRequired();

        builder.Property(e => e.AllowedScopeRules)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.HasMany(e => e.ScanJobs)
            .WithOne(s => s.Engagement)
            .HasForeignKey(s => s.EngagementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
