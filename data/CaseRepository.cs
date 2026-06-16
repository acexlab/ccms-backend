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

    public async Task<IEnumerable<Case>> GetCasesForBankAsync(string? bankCode = null)
    {
        var query = _context.Cases
            .Include(c => c.Defendant)
            .Include(c => c.Complainant)
            .AsQueryable();

        if (!string.IsNullOrEmpty(bankCode))
        {
            query = query.Where(c => c.Defendant != null && c.Defendant.BankName == bankCode);
        }

        return await query.ToListAsync();
    }
}