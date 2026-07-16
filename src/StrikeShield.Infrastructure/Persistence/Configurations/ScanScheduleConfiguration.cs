using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class ScanScheduleConfiguration : IEntityTypeConfiguration<ScanSchedule>
{
    public void Configure(EntityTypeBuilder<ScanSchedule> builder)
    {
        builder.ToTable("ScanSchedules");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CronExpression)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Enabled)
            .IsRequired();

        builder.Property(s => s.CreatedBy)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.LastSkipReason)
            .HasMaxLength(1000);

        builder.HasOne(s => s.Engagement)
            .WithMany()
            .HasForeignKey(s => s.EngagementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Target)
            .WithMany()
            .HasForeignKey(s => s.TargetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Playbook)
            .WithMany()
            .HasForeignKey(s => s.PlaybookId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.EngagementId);
    }
}
