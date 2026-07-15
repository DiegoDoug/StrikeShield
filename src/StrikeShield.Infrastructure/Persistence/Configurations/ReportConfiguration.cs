using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Type)
            .IsRequired();

        builder.Property(r => r.MarkdownContent)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(r => r.PdfContent)
            .IsRequired()
            .HasColumnType("bytea");

        builder.Property(r => r.GeneratedAt)
            .IsRequired();

        builder.HasOne(r => r.Engagement)
            .WithMany(e => e.Reports)
            .HasForeignKey(r => r.EngagementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.EngagementId);
    }
}
