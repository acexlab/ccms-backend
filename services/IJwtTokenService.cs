/*
 * File: IJwtTokenService.cs
 * Description: Interface defining JWT token generation.
 * To Implement: Implement in Services.
 */

using ccms_backend.models;

namespace ccms_backend.services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}
