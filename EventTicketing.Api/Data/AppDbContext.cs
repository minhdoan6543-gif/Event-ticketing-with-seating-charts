using EventTicketing.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventTicketing.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(ur => new { ur.UserId, ur.RoleId });

            entity.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            entity.HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);
        });

        // Seed Roles
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Buyer" },
            new Role { Id = 2, Name = "Organizer" },
            new Role { Id = 3, Name = "GateStaff" },
            new Role { Id = 4, Name = "Accountant" },
            new Role { Id = 5, Name = "Admin" }
        );

        // Seed Users (password_hash should be argon2id, for seed we can use a dummy hash or leave it for later proper generation)
        modelBuilder.Entity<User>().HasData(
            new User
            {
                Id = 1,
                Email = "admin@example.com",
                Name = "Admin User",
                PasswordHash = "dummy_hash_for_now",
                Status = "Active",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new User
            {
                Id = 2,
                Email = "organizer@example.com",
                Name = "Organizer User",
                PasswordHash = "dummy_hash_for_now",
                Status = "Active",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        modelBuilder.Entity<UserRole>().HasData(
            new UserRole { UserId = 1, RoleId = 5 }, // Admin
            new UserRole { UserId = 2, RoleId = 2 }  // Organizer
        );
    }
}
