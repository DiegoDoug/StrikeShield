using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class StepRunConfiguration : IEntityTypeConfiguration<StepRun>
{
    public void Configure(EntityTypeBuilder<StepRun> builder)
    {
        builder.ToTable("StepRuns");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Status)
            .IsRequired();

        builder.Property(s => s.ContainerId)
            .HasMaxLength(100);

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasMany(s => s.Artifacts)
            .WithOne(a => a.StepRun)
            .HasForeignKey(a => a.StepRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
