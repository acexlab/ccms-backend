/*
 * File: CaseRepository.cs
 * Description: Entity Framework Core repository implementation for Case operations.
 * To Implement: Eager load Documents and Response relationships in queries.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ccms_backend.models;

namespace ccms_backend.data;

public class CaseRepository : ICaseRepository
{
    private readonly AppDbContext _context;

    public CaseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Case?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Response)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<List<Case>> GetByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return await _context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Response)
            .Where(c => c.CreatedByUserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Case>> GetByBankCodeAsync(string bankCode, CancellationToken ct = default)
    {
        return await _context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Response)
            .Where(c => c.BankCode == bankCode)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<Case>> GetByStatusAsync(CaseStatus status, CancellationToken ct = default)
    {
        return await _context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Response)
            .Where(c => c.Status == status)
            .ToListAsync(ct);
    }

    public async Task<int> GetTodayCaseCountAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.Cases
            .CountAsync(c => c.CreatedAt >= today, ct);
    }

    public async Task AddAsync(Case @case, CancellationToken ct = default)
    {
        await _context.Cases.AddAsync(@case, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Case @case, CancellationToken ct = default)
    {
        _context.Cases.Update(@case);
        await _context.SaveChangesAsync(ct);
    }
}
// Note for developer: Change tracking handles cascading changes for nested properties appropriately.
