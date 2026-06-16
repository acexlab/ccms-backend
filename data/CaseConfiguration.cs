using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => c.CaseNumber)
            .IsUnique();

        builder.Property(c => c.OrderType)
            .HasConversion<string>();

        builder.Property(c => c.Status)
            .HasConversion<string>();

        builder.Property(c => c.FreezeAmount)
            .HasPrecision(18, 2);
    }
}