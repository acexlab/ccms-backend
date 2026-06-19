using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
