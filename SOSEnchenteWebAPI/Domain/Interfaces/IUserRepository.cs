using SOSEnchenteWebAPI.Domain.Models;

namespace SOSEnchenteWebAPI.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<User> CreateUserAsync(User user);
    Task<bool> UserExistsAsync(string email);
    Task SaveChangesAsync();
    Task<User?> GetUserByIdAsync(int id);
    Task<List<EmergencyContact>> GetUserEmergencyContactsAsync(int userId);
    Task<EmergencyContact> AddEmergencyContactAsync(EmergencyContact emergencyContact);
    Task<List<User>> GetAllUsersAsync();
    Task<List<User>> GetDependentsAsync(int userId);
    Task<Dependent> AddDependentAsync(Dependent dependent);
}
