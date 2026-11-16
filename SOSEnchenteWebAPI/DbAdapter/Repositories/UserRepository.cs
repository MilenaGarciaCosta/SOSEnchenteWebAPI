using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.DbAdapter;
using SOSEnchenteWebAPI.Domain.Interfaces;
using SOSEnchenteWebAPI.Domain.Models;

namespace SOSEnchenteWebAPI.DbAdapter.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.HealthProfile)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> UserExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.HealthProfile)
            .FirstOrDefaultAsync(u => u.Id == id);
    }
}
