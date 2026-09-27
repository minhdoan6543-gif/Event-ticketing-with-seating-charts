using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;
using TicketBooking.Api.Data;
using TicketBooking.Api.HealthChecks;
using Xunit;

namespace TicketBooking.Tests;

public class PostgresRedisConnectivityTests
{
    [Fact]
    public async Task PostgresHealthCheck_WhenCanConnect_ReturnsHealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        var check = new PostgresHealthCheck(dbContext);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("PostgreSQL connection verified successfully", result.Description);
    }

    [Fact]
    public async Task PostgresHealthCheck_WhenCannotConnect_ReturnsUnhealthyWithoutLeakingSecrets()
    {
        // Arrange: Unreachable connection string with sensitive password
        var sensitivePassword = "SuperSecretPassword123!";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql($"Host=127.0.0.1;Port=59998;Database=test;Username=user;Password={sensitivePassword};Timeout=1")
            .Options;

        using var dbContext = new ApplicationDbContext(options);
        var check = new PostgresHealthCheck(dbContext);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        // Đảm bảo mật khẩu không bao giờ xuất hiện trong output mô tả lỗi
        Assert.DoesNotContain(sensitivePassword, result.Description ?? string.Empty);
    }

    [Fact]
    public async Task RedisHealthCheck_WhenMultiplexerIsNull_ReturnsUnhealthy()
    {
        // Arrange
        var check = new RedisHealthCheck(null);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not registered", result.Description);
    }

    [Fact]
    public async Task RedisHealthCheck_WhenDisconnected_ReturnsUnhealthy()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.IsConnected).Returns(false);

        var check = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("currently disconnected", result.Description);
    }

    [Fact]
    public async Task RedisHealthCheck_WhenPingSucceeds_ReturnsHealthy()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(2.5));

        var check = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("responsive", result.Description);
    }

    [Fact]
    public async Task RedisHealthCheck_WhenPingThrows_ReturnsUnhealthy()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "Network unreachable"));

        var check = new RedisHealthCheck(mockRedis.Object);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Redis ping failed", result.Description);
    }
}
