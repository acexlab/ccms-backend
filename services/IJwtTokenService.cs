using ccms_backend.models;

namespace ccms_backend.services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}