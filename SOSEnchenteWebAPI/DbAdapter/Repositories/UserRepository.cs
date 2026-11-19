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

    public async Task<List<EmergencyContact>> GetUserEmergencyContactsAsync(int userId)
    {
        return await _context.EmergencyContacts
            .Where(ec => ec.UserId == userId)
            .ToListAsync();
    }

    public async Task<EmergencyContact> AddEmergencyContactAsync(EmergencyContact emergencyContact)
    {
        _context.EmergencyContacts.Add(emergencyContact);
        await _context.SaveChangesAsync();
        return emergencyContact;
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .Include(u => u.HealthProfile)
            .Where(u => !u.IsAdmin)
            .ToListAsync();
    }

    public async Task<List<User>> GetDependentsAsync(int userId)
    {
        return await _context.Dependents
            .Where(d => d.UserId == userId)
            .Select(d => d.DependentUser)
            .ToListAsync();
    }

    public async Task<Dependent> AddDependentAsync(Dependent dependent)
    {
        _context.Dependents.Add(dependent);
        await _context.SaveChangesAsync();
        return dependent;
    }
}
