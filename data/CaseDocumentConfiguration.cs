using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseDocumentConfiguration : IEntityTypeConfiguration<CaseDocument>
{
    public void Configure(EntityTypeBuilder<CaseDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .HasConversion<string>();

        builder.Property(d => d.FilePath)
            .HasMaxLength(500);
    }
}