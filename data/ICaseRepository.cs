using System.Collections.Generic;
using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface ICaseRepository
{
    Task<IEnumerable<Case>> GetCasesForBankAsync(string? bankCode = null);
}