using System.Linq;
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
}