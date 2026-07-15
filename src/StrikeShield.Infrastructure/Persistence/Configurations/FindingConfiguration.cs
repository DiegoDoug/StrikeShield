using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("Findings");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.SourceTool)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(f => f.Description)
            .HasColumnType("text");

        builder.Property(f => f.Severity)
            .IsRequired();

        builder.Property(f => f.CvssVector)
            .HasMaxLength(100);

        builder.Property(f => f.CweIds)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(f => f.CveIds)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(f => f.OwaspCategory)
            .HasMaxLength(200);

        builder.Property(f => f.MitreAttackTechniques)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(f => f.AffectedAsset)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(f => f.PocCode)
            .HasColumnType("text");

        builder.Property(f => f.ReproSteps)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(f => f.RecommendedFix)
            .HasColumnType("text");

        builder.Property(f => f.VerificationSteps)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(f => f.Status)
            .IsRequired();

        builder.Property(f => f.DedupeFingerprint)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.FirstSeenAt)
            .IsRequired();

        builder.Property(f => f.LastSeenAt)
            .IsRequired();

        builder.HasIndex(f => f.DedupeFingerprint);

        builder.HasOne(f => f.ScanJob)
            .WithMany(s => s.Findings)
            .HasForeignKey(f => f.ScanJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.StepRun)
            .WithMany(s => s.Findings)
            .HasForeignKey(f => f.StepRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.CorrelationGroup)
            .WithMany(c => c.Findings)
            .HasForeignKey(f => f.CorrelationGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(f => f.Evidence)
            .WithOne(e => e.Finding)
            .HasForeignKey(e => e.FindingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
