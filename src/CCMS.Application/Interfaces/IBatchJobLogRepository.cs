using System.Collections.Generic;
using System.Threading.Tasks;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Interfaces;

public interface IBatchJobLogRepository
{
    Task<BatchJobLog?> GetLastRunAsync();
    Task<BatchJobLog> AddLogAsync(BatchJobLog log);
    Task<IEnumerable<BatchJobLog>> GetAllSuccessfulRunsAsync();
    Task<IEnumerable<BatchJobLog>> GetAllRunsAsync();
}
