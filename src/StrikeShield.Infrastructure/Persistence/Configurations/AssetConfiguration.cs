using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type)
            .IsRequired();

        builder.Property(a => a.Value)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.Metadata)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.HasOne(a => a.Target)
            .WithMany(t => t.Assets)
            .HasForeignKey(a => a.TargetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.DiscoveredByStepRun)
            .WithMany(s => s.DiscoveredAssets)
            .HasForeignKey(a => a.DiscoveredByStepRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
