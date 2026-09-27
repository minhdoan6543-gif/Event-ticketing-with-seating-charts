using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using TicketBooking.Api.Data;
using TicketBooking.Api.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình Cơ sở dữ liệu PostgreSQL qua Entity Framework Core
var postgresConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=ticket_booking_db;Username=ticket_user;Password=TicketSecureDevPassword2026!;Timeout=15";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
});

// 2. Kiểm tra nếu tham số dòng lệnh là chạy Migration
if (args.Contains("--migrate-database"))
{
    Console.WriteLine("==================================================");
    Console.WriteLine("[MIGRATION RUNNER] Đang khởi chạy áp dụng migration...");
    Console.WriteLine("==================================================");

    using var migrationApp = builder.Build();
    using var scope = migrationApp.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync()).ToList();
        Console.WriteLine($"[MIGRATION RUNNER] Số lượng migrations đang chờ xử lý: {pendingMigrations.Count}");
        foreach (var m in pendingMigrations)
        {
            Console.WriteLine($" -> Chuẩn bị áp dụng: {m}");
        }

        await db.Database.MigrateAsync();
        Console.WriteLine("[MIGRATION RUNNER] Migration hoàn tất thành công 100%!");
        return 0; // Thoát thành công cho service migration trong Docker Compose
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[MIGRATION RUNNER ERROR] Lỗi khi thực hiện migration: {ex.Message}");
        return 1; // Thoát với mã lỗi để ngăn backend khởi động
    }
}

// 3. Cấu hình Redis Cache
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379,password=RedisSecureDevPassword2026!,abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var configOptions = ConfigurationOptions.Parse(redisConnectionString);
        configOptions.AbortOnConnectFail = false;
        configOptions.ConnectTimeout = 5000;
        configOptions.SyncTimeout = 5000;
        return ConnectionMultiplexer.Connect(configOptions);
    }
    catch (Exception ex)
    {
        logger.LogWarning("Không thể kết nối ngay tới Redis tại thời điểm khởi tạo: {Message}", ex.Message);
        var configOptions = ConfigurationOptions.Parse(redisConnectionString);
        configOptions.AbortOnConnectFail = false;
        return ConnectionMultiplexer.Connect(configOptions);
    }
});

// 4. Đăng ký Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy("Tiến trình ứng dụng đang hoạt động."), tags: new[] { "live" })
    .AddCheck<PostgresHealthCheck>("postgresql", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

// 5. Cấu hình CORS để phục vụ React Frontend trong môi trường Dev & Staging
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

// 6. Endpoint Liveness Check (kiểm tra tiến trình đang chạy)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// 7. Endpoint Readiness Check (kiểm tra PostgreSQL và Redis - trả 503 khi lỗi)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

// 8. Thông tin cơ bản về hệ thống (User Story S-01)
app.MapGet("/", () => Results.Ok(new
{
    projectName = "Bán vé sự kiện có sơ đồ ghế",
    story = "S-01: Khung ứng dụng chạy được trên staging",
    status = "Running",
    environment = app.Environment.EnvironmentName,
    endpoints = new
    {
        liveness = "/health/live",
        readiness = "/health/ready"
    },
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
return 0;

// Hỗ trợ WebApplicationFactory trong Unit/Integration Tests
public partial class Program { }
