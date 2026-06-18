using System.Threading.Tasks;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;

namespace CCMS.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
}