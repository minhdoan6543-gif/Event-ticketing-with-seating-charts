using Microsoft.Extensions.Diagnostics.HealthChecks;
using TicketBooking.Api.Data;

namespace TicketBooking.Api.HealthChecks;

public class PostgresHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _dbContext;

    public PostgresHealthCheck(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Kiểm tra khả năng kết nối tới PostgreSQL mà không lộ thông tin nhạy cảm
            bool canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("PostgreSQL connection verified successfully.");
            }

            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable: CanConnectAsync returned false.");
        }
        catch (Exception ex)
        {
            // Chỉ trả về thông báo lỗi ngắn gọn, không ghi chuỗi kết nối chứa mật khẩu ra response
            return HealthCheckResult.Unhealthy($"PostgreSQL connection failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}
