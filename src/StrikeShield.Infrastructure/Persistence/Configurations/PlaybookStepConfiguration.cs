using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class PlaybookStepConfiguration : IEntityTypeConfiguration<PlaybookStep>
{
    public void Configure(EntityTypeBuilder<PlaybookStep> builder)
    {
        builder.ToTable("PlaybookSteps");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Order)
            .IsRequired();

        builder.Property(s => s.ToolName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.ImageRepository)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(s => s.ImageTag)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.StepKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.DependsOn)
            .IsRequired()
            .HasColumnType("text[]");

        builder.Property(s => s.Condition)
            .IsRequired();

        builder.Property(s => s.ArgsTemplate)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(s => s.TimeoutSeconds)
            .IsRequired();

        builder.Property(s => s.MemoryLimitBytes)
            .IsRequired();

        builder.Property(s => s.NanoCpus)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.HasIndex(s => new { s.PlaybookId, s.StepKey })
            .IsUnique();

        builder.HasMany(s => s.StepRuns)
            .WithOne(sr => sr.PlaybookStep)
            .HasForeignKey(sr => sr.PlaybookStepId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
