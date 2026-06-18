using System.Collections.Generic;
using System.Threading.Tasks;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Interfaces;

public interface ICaseRepository
{
    Task<IEnumerable<Case>> GetCasesForBankAsync();
}
