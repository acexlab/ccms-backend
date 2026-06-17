using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;

namespace CCMS.Application.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Case> Cases { get; }
    DbSet<Complainant> Complainants { get; }
    DbSet<Defendant> Defendants { get; }
    DbSet<CaseDocument> CaseDocuments { get; }
    DbSet<BankCustomer> BankCustomers { get; }
    DbSet<CaseValidationResult> CaseValidationResults { get; }
    DbSet<CaseResponse> CaseResponses { get; }
    DbSet<BatchJobLog> BatchJobLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
