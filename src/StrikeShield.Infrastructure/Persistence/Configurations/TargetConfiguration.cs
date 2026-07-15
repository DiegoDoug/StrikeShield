using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class TargetConfiguration : IEntityTypeConfiguration<Target>
{
    public void Configure(EntityTypeBuilder<Target> builder)
    {
        builder.ToTable("Targets");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .IsRequired();

        builder.Property(t => t.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.HasMany(t => t.ScanJobs)
            .WithOne(s => s.Target)
            .HasForeignKey(s => s.TargetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
