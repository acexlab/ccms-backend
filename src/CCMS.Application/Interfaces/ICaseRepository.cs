using System.Collections.Generic;
using System.Threading.Tasks;
using CCMS.Domain.Entities;

namespace CCMS.Application.Interfaces;

public interface ICaseRepository
{
    Task<IEnumerable<Case>> GetCasesForBankAsync();
}
