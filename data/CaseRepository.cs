using System.Collections.Generic;
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

    public async Task<IEnumerable<Case>> GetCasesForBankAsync()
    {
        return await _context.Cases
            .Include(c => c.Defendant)
            .Include(c => c.Complainant)
            .ToListAsync();
    }
}