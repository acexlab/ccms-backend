using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface IBatchJobLogRepository
{
    Task<BatchJobLog?> GetLastRunAsync();
    Task<BatchJobLog> AddLogAsync(BatchJobLog log);
}