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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure User Role enum conversion
        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        // Configure Case OrderType and Status conversion
        modelBuilder.Entity<Case>()
            .Property(c => c.OrderType)
            .HasConversion<string>();

        modelBuilder.Entity<Case>()
            .Property(c => c.Status)
            .HasConversion<string>();

        // Configure CaseDocument DocumentType conversion
        modelBuilder.Entity<CaseDocument>()
            .Property(d => d.DocumentType)
            .HasConversion<string>();

        // Configure BankCustomer AccountStatus conversion
        modelBuilder.Entity<BankCustomer>()
            .Property(b => b.AccountStatus)
            .HasConversion<string>();

        // Configure CaseValidationResult MatchedOn conversion
        modelBuilder.Entity<CaseValidationResult>()
            .Property(v => v.MatchedOn)
            .HasConversion<string>();

        // Configure CaseResponse ResponseType conversion
        modelBuilder.Entity<CaseResponse>()
            .Property(r => r.ResponseType)
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