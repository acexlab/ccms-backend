using CCMS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Infrastructure.Data;

public class AppDbContext : DbContext, IAppDbContext
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

    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _currentTransaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) return;
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }
}
