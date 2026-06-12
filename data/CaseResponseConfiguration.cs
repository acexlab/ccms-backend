/*
 * File: CaseResponseConfiguration.cs
 * Description: EF Core configuration mapping CaseResponse entity.
 * To Implement: Enforce unique 1-to-1 index on CaseId, set precision for amount.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseResponseConfiguration : IEntityTypeConfiguration<CaseResponse>
{
    public void Configure(EntityTypeBuilder<CaseResponse> builder)
    {
        builder.ToTable("CaseResponses");

        builder.HasKey(cr => cr.Id);

        builder.HasIndex(cr => cr.CaseId)
            .IsUnique(); // Enforces 1-to-1 response constraint at DB level

        builder.Property(cr => cr.ResponseType)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(cr => cr.ReportedAmount)
            .HasPrecision(18, 2);

        builder.Property(cr => cr.Remarks)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(cr => cr.IsSystemGenerated)
            .IsRequired();

        builder.Property(cr => cr.RespondedAt)
            .IsRequired();
    }
}
