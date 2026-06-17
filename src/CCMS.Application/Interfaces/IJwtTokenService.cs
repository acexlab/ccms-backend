using CCMS.Domain.Entities;

namespace CCMS.Application.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
