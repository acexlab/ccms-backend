/*
 * File: BatchJobLogRepository.cs
 * Description: EF Core repository implementation to audit Batch Validation execution logs.
 * To Implement: GetLastRunAsync orders logs descending by RunAt and takes the first log.
 */

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public class BatchJobLogRepository : IBatchJobLogRepository
{
    private readonly AppDbContext _context;

    public BatchJobLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(BatchJobLog log, CancellationToken ct = default)
    {
        await _context.BatchJobLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<BatchJobLog?> GetLastRunAsync(CancellationToken ct = default)
    {
        return await _context.BatchJobLogs
            .OrderByDescending(l => l.RunAt)
            .FirstOrDefaultAsync(ct);
    }
}
