/*
 * File: BankCustomerConfiguration.cs
 * Description: EF Core configuration mapping BankCustomer entity.
 * To Implement: Create indexes on AccountNumber, AadhaarNumber, PanNumber, and BankCode.
 */

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ccms_backend.models;

namespace ccms_backend.data;

public class BankCustomerConfiguration : IEntityTypeConfiguration<BankCustomer>
{
    public void Configure(EntityTypeBuilder<BankCustomer> builder)
    {
        builder.ToTable("BankCustomers");

        builder.HasKey(bc => bc.Id);

        builder.Property(bc => bc.AccountNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(bc => bc.AadhaarNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(bc => bc.PanNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(bc => bc.Balance)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(bc => bc.AccountStatus)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(bc => bc.BankCode)
            .IsRequired()
            .HasMaxLength(20);

        // Crucial performance indexes for the background batch job matching algorithm
        builder.HasIndex(bc => bc.AccountNumber);
        builder.HasIndex(bc => bc.AadhaarNumber);
        builder.HasIndex(bc => bc.PanNumber);
        builder.HasIndex(bc => bc.BankCode);
    }
}
// Note for developer: These indexes are critical to avoid full-table scans during bulk batch execution.
