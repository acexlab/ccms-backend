/*
 * File: AppDbContext.cs
 * Description: EF Core database context representing CCMS database connection and DbSet schemas.
 * To Implement: Configure DbSets and Fluent API schema overrides.
 */

using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Case> Cases { get; set; } = null!;
    public DbSet<CaseDocument> CaseDocuments { get; set; } = null!;
    public DbSet<CaseResponse> CaseResponses { get; set; } = null!;
    public DbSet<BankCustomer> BankCustomers { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<BatchJobLog> BatchJobLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
