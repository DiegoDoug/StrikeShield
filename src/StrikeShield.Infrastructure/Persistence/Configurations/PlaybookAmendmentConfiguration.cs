using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class PlaybookAmendmentConfiguration : IEntityTypeConfiguration<PlaybookAmendment>
{
    public void Configure(EntityTypeBuilder<PlaybookAmendment> builder)
    {
        builder.ToTable("PlaybookAmendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Rationale)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(a => a.ProposedArgsTemplate)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(a => a.Status)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.DecidedBy)
            .HasMaxLength(200);

        builder.HasOne(a => a.ScanJob)
            .WithMany(s => s.Amendments)
            .HasForeignKey(a => a.ScanJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ProposedByStepRun)
            .WithMany()
            .HasForeignKey(a => a.ProposedByStepRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.TargetPlaybookStep)
            .WithMany()
            .HasForeignKey(a => a.TargetPlaybookStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.ScanJobId);
    }
}
