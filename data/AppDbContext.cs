using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Case> Cases { get; set; } = null!;
    public DbSet<Complainant> Complainants { get; set; } = null!;
    public DbSet<Defendant> Defendants { get; set; } = null!;
    public DbSet<CaseDocument> CaseDocuments { get; set; } = null!;
    public DbSet<BankCustomer> BankCustomers { get; set; } = null!;
    public DbSet<CaseValidationResult> CaseValidationResults { get; set; } = null!;
    public DbSet<CaseResponse> CaseResponses { get; set; } = null!;
    public DbSet<BatchJobLog> BatchJobLogs { get; set; } = null!;
    public DbSet<Bank> Banks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Configure CaseValidationResult MatchedOn conversion
        modelBuilder.Entity<CaseValidationResult>()
            .Property(v => v.MatchedOn)
            .HasConversion<string>();

        // Configure BatchJobLog TriggeredBy and Status conversion
        modelBuilder.Entity<BatchJobLog>()
            .Property(l => l.TriggeredBy)
            .HasConversion<string>();

        modelBuilder.Entity<BatchJobLog>()
            .Property(l => l.Status)
            .HasConversion<string>();
    }
}