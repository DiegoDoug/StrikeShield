using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class ScanJobConfiguration : IEntityTypeConfiguration<ScanJob>
{
    public void Configure(EntityTypeBuilder<ScanJob> builder)
    {
        builder.ToTable("ScanJobs");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasOne(s => s.Playbook)
            .WithMany(p => p.ScanJobs)
            .HasForeignKey(s => s.PlaybookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.StepRuns)
            .WithOne(sr => sr.ScanJob)
            .HasForeignKey(sr => sr.ScanJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
