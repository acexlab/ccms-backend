/*
 * File: CaseDocumentConfiguration.cs
 * Description: EF Core configuration mapping CaseDocument entity to database schema.
 * To Implement: Convert DocumentType enum to string, apply max length to file path.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseDocumentConfiguration : IEntityTypeConfiguration<CaseDocument>
{
    public void Configure(EntityTypeBuilder<CaseDocument> builder)
    {
        builder.ToTable("CaseDocuments");

        builder.HasKey(cd => cd.Id);

        builder.Property(cd => cd.DocumentType)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(cd => cd.FileName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(cd => cd.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(cd => cd.UploadedAt)
            .IsRequired();
    }
}
