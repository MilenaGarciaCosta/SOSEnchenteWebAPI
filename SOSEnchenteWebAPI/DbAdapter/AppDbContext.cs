namespace SOSEnchenteWebAPI.DbAdapter;

using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.Domain.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<HealthProfile> HealthProfiles { get; set; }
    public DbSet<EmergencyContact> EmergencyContacts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .Property(u => u.Password)
            .HasColumnName("Senha");

        modelBuilder.Entity<User>()
            .HasOne(u => u.HealthProfile)
            .WithOne()
            .HasForeignKey<HealthProfile>(hp => hp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.EmergencyContacts)
            .WithOne()
            .HasForeignKey(ec => ec.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
