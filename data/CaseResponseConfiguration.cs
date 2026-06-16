using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseResponseConfiguration : IEntityTypeConfiguration<CaseResponse>
{
    public void Configure(EntityTypeBuilder<CaseResponse> builder)
    {
        builder.HasKey(cr => cr.Id);

        builder.HasIndex(cr => cr.CaseId)
            .IsUnique();

        builder.Property(cr => cr.FreezeAmountApplied)
            .HasPrecision(18, 2);

        builder.Property(cr => cr.BalanceReported)
            .HasPrecision(18, 2);

        builder.Property(cr => cr.ResponseType)
            .HasConversion<string>();

        builder.HasOne(cr => cr.RespondedByUser)
            .WithMany()
            .HasForeignKey(cr => cr.RespondedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}