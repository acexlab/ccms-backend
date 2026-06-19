using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;
using CCMS.Application.Interfaces;

namespace CCMS.Infrastructure.Data;

public class BatchJobLogRepository : IBatchJobLogRepository
{
    private readonly AppDbContext _context;

    public BatchJobLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<BatchJobLog?> GetLastRunAsync()
    {
        return await _context.BatchJobLogs
            .OrderByDescending(b => b.StartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<BatchJobLog> AddLogAsync(BatchJobLog log)
    {
        _context.BatchJobLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<IEnumerable<BatchJobLog>> GetAllSuccessfulRunsAsync()
    {
        return await _context.BatchJobLogs
            .Where(l => l.Status == BatchJobStatus.Success)
            .ToListAsync();
    }

    public async Task<IEnumerable<BatchJobLog>> GetAllRunsAsync()
    {
        return await _context.BatchJobLogs
            .OrderByDescending(l => l.StartTime)
            .ToListAsync();
    }
}
