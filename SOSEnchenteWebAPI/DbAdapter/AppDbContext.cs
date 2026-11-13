namespace SOSEnchenteWebAPI.DbAdapter;

using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.Domain.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<HealthProfile> HealthProfiles { get; set; }
    public DbSet<EmergencyContact> EmergencyContacts { get; set; }
}
