using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class BankCustomerConfiguration : IEntityTypeConfiguration<BankCustomer>
{
    public void Configure(EntityTypeBuilder<BankCustomer> builder)
    {
        builder.HasKey(bc => bc.Id);

        builder.HasIndex(bc => bc.AccountNumber);
        builder.HasIndex(bc => bc.AadhaarNumber);
        builder.HasIndex(bc => bc.PANNumber);
        builder.HasIndex(bc => bc.BankCode);

        builder.Property(bc => bc.TotalBalance)
            .HasPrecision(18, 2);

        builder.Property(bc => bc.AvailableBalance)
            .HasPrecision(18, 2);

        builder.Property(bc => bc.FrozenAmount)
            .HasPrecision(18, 2);

        builder.Property(bc => bc.AccountStatus)
            .HasConversion<string>();
    }
}