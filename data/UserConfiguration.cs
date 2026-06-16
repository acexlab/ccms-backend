using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.Role)
            .HasConversion<string>();
    }
}