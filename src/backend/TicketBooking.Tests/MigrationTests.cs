using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TicketBooking.Api.Data;
using TicketBooking.Api.Data.Migrations;
using Xunit;

namespace TicketBooking.Tests;

public class MigrationTests
{
    [Fact]
    public void InitialMigration_Up_CreatesSystemConfigsTableAndSeedsData()
    {
        // Arrange
        var migration = new InitialSystemConfig();
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        // Act: Gọi phương thức Up của Migration
        var upMethod = typeof(InitialSystemConfig).GetMethod(
            "Up",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(upMethod);
        upMethod.Invoke(migration, new object[] { migrationBuilder });

        // Assert: Kiểm tra danh sách operations khi chạy tiến
        var operations = migrationBuilder.Operations;
        Assert.NotEmpty(operations);

        // 1. Phải có CreateTableOperation cho bảng "system_configs"
        var createTableOp = operations.OfType<CreateTableOperation>().FirstOrDefault(o => o.Name == "system_configs");
        Assert.NotNull(createTableOp);
        Assert.Equal("system_configs", createTableOp.Name);
        Assert.Equal("key", createTableOp.PrimaryKey?.Columns[0]);

        // Kiểm tra các cột bắt buộc
        var columns = createTableOp.Columns;
        Assert.Contains(columns, c => c.Name == "key" && !c.IsNullable);
        Assert.Contains(columns, c => c.Name == "value" && !c.IsNullable);
        Assert.Contains(columns, c => c.Name == "description" && c.IsNullable);
        Assert.Contains(columns, c => c.Name == "updated_at" && !c.IsNullable);

        // 2. Phải có InsertDataOperation khởi tạo dữ liệu kỹ thuật
        var insertOp = operations.OfType<InsertDataOperation>().FirstOrDefault(o => o.Table == "system_configs");
        Assert.NotNull(insertOp);
        Assert.Contains("App:Initialized", insertOp.Values.Cast<object>().Select(v => v?.ToString()));
    }

    [Fact]
    public void InitialMigration_Down_DropsSystemConfigsTable()
    {
        // Arrange
        var migration = new InitialSystemConfig();
        var migrationBuilder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        // Act: Gọi phương thức Down của Migration
        var downMethod = typeof(InitialSystemConfig).GetMethod(
            "Down",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(downMethod);
        downMethod.Invoke(migration, new object[] { migrationBuilder });

        // Assert: Kiểm tra danh sách operations khi chạy lùi
        var operations = migrationBuilder.Operations;
        Assert.Single(operations);

        var dropTableOp = operations.OfType<DropTableOperation>().FirstOrDefault();
        Assert.NotNull(dropTableOp);
        Assert.Equal("system_configs", dropTableOp.Name);
    }

    [Fact]
    public async Task Migration_Roundtrip_ForwardBackwardForward_OnTestDatabaseIfAvailable()
    {
        // Đọc chuỗi kết nối từ biến môi trường TEST_POSTGRES_CONNECTION nếu có (ví dụ trong CI hoặc local docker)
        var testConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(testConn))
        {
            // Bỏ qua roundtrip trên live DB nếu môi trường hiện tại chưa khởi động container PostgreSQL
            return;
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(testConn)
            .Options;

        using var context = new ApplicationDbContext(options);
        var migrator = context.Database.GetService<IMigrator>();

        // 1. Chạy tiến (Up to latest)
        await migrator.MigrateAsync();
        var seed = await context.SystemConfigs.FindAsync("App:Initialized");
        Assert.NotNull(seed);
        Assert.Equal("True", seed.Value);

        // 2. Chạy lùi (Rollback to initial state "0")
        await migrator.MigrateAsync("0");

        // 3. Chạy tiến lại (Up again)
        await migrator.MigrateAsync();
        var seedReapplied = await context.SystemConfigs.FindAsync("App:Initialized");
        Assert.NotNull(seedReapplied);
        Assert.Equal("True", seedReapplied.Value);
    }
}
