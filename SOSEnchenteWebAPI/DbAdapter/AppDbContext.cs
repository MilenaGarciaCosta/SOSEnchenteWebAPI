namespace SOSEnchenteWebAPI.DbAdapter;

using Microsoft.EntityFrameworkCore;
using SOSEnchenteWebAPI.Domain.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }

    public DbSet<User> Users { get; set; }
    public DbSet<HealthProfile> HealthProfiles { get; set; }
    public DbSet<EmergencyContact> EmergencyContacts { get; set; }
    public DbSet<Dependent> Dependents { get; set; }
    public DbSet<SecurityAuditEvent> SecurityAuditEvents { get; set; }

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

        modelBuilder.Entity<Dependent>()
            .HasKey(d => new { d.UserId, d.DependentId });

        modelBuilder.Entity<Dependent>()
            .HasOne(d => d.User)
            .WithMany(u => u.Dependents)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Dependent>()
            .HasOne(d => d.DependentUser)
            .WithMany()
            .HasForeignKey(d => d.DependentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
