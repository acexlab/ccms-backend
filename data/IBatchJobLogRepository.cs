/*
 * File: IBatchJobLogRepository.cs
 * Description: Interface defining data operations for auditing Batch Validation executions.
 * To Implement: Implement in Repository layer.
 */

using System.Threading;
using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface IBatchJobLogRepository
{
    Task AddAsync(BatchJobLog log, CancellationToken ct = default);
    Task<BatchJobLog?> GetLastRunAsync(CancellationToken ct = default);
}
// Note: Displays execution time metrics.
