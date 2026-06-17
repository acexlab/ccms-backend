using System.Threading.Tasks;
using CCMS.Domain.Entities;

namespace CCMS.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
}