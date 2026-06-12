/*
 * File: IUserRepository.cs
 * Description: Interface defining data operations for querying User records.
 * To Implement: Implement in Repository layer.
 */

using System.Threading;
using System.Threading.Tasks;
using ccms_backend.models;

namespace ccms_backend.data;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
}
// Note: Used heavily in login validation and token parsing pipelines.
