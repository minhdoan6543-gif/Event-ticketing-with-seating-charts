using Microsoft.EntityFrameworkCore;
using TicketBooking.Api.Data.Entities;

namespace TicketBooking.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.UpdatedAt).IsRequired();

            // Khởi tạo một dòng cấu hình kỹ thuật để kiểm chứng dữ liệu ban đầu
            entity.HasData(new SystemConfig
            {
                Key = "App:Initialized",
                Value = "True",
                Description = "Dự án Bán vé sự kiện khởi tạo thành công khung S-01",
                UpdatedAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero)
            });
        });
    }
}
