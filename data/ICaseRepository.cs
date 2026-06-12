/*
 * File: ICaseRepository.cs
 * Description: Interface defining data operations for Case entities.
 * To Implement: Implement using Entity Framework Core DbContext.
 */

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface ICaseRepository
{
    Task<Case?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<Case>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<List<Case>> GetByBankCodeAsync(string bankCode, CancellationToken ct = default);
    Task<List<Case>> GetByStatusAsync(CaseStatus status, CancellationToken ct = default);
    Task<int> GetTodayCaseCountAsync(CancellationToken ct = default); // For sequential numbering
    Task AddAsync(Case @case, CancellationToken ct = default);
    Task UpdateAsync(Case @case, CancellationToken ct = default);
}
