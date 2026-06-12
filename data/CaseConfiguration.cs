/*
 * File: CaseConfiguration.cs
 * Description: EF Core configuration mapping the Case entity to the Cases table.
 * To Implement: Unique index on CaseNumber, string conversion for enums.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CaseNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.HasIndex(c => c.CaseNumber)
            .IsUnique();

        builder.Property(c => c.OrderType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(c => c.ComplainantName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.DefendantName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.DefendantAadhaar)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.DefendantPan)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.DefendantAccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.BankCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.FreezeAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.MatchedAccountNumber)
            .HasMaxLength(50);

        builder.Property(c => c.MatchedBalance)
            .HasPrecision(18, 2);

        builder.Property(c => c.MatchedAccountStatus)
            .HasMaxLength(30);

        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Case)
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Response)
            .WithOne(r => r.Case)
            .HasForeignKey<CaseResponse>(r => r.CaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
