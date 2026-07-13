using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrikeShield.Domain.Entities;

namespace StrikeShield.Infrastructure.Persistence.Configurations;

public class PlaybookConfiguration : IEntityTypeConfiguration<Playbook>
{
    public void Configure(EntityTypeBuilder<Playbook> builder)
    {
        builder.ToTable("Playbooks");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.Slug)
            .IsUnique();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(2000);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.HasMany(p => p.Steps)
            .WithOne(s => s.Playbook)
            .HasForeignKey(s => s.PlaybookId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
