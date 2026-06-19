using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CCMS.Domain.Entities;
using CCMS.Domain.Enums;
using CCMS.Application.Interfaces;

namespace CCMS.Infrastructure.Data;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }
}
