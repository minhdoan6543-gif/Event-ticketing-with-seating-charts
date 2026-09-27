using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using StackExchange.Redis;
using TicketBooking.Api.Data;
using TicketBooking.Api.HealthChecks;
using Xunit;

namespace TicketBooking.Tests;

public class HealthCheckTests
{
    [Fact]
    public async Task LivenessEndpoint_Returns200_AndHealthyStatus()
    {
        // Arrange
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Healthy", status);
    }

    [Fact]
    public async Task ReadinessEndpoint_WhenDependenciesAreHealthy_Returns200OK()
    {
        // Arrange
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(5));

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    // Thay thế DbContext bằng InMemory để CanConnectAsync trả về true
                    var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (dbDescriptor != null) services.Remove(dbDescriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("HealthCheckHealthyDb");
                    });

                    // Thay thế Redis multiplexer bằng mock
                    var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
                    if (redisDescriptor != null) services.Remove(redisDescriptor);

                    services.AddSingleton<IConnectionMultiplexer>(mockRedis.Object);
                });
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Healthy", status);

        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal("Healthy", entries.GetProperty("postgresql").GetProperty("status").GetString());
        Assert.Equal("Healthy", entries.GetProperty("redis").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessEndpoint_WhenRedisIsUnhealthy_Returns503ServiceUnavailable()
    {
        // Arrange: Mock Redis disconnected
        var mockRedis = new Mock<IConnectionMultiplexer>();
        mockRedis.Setup(r => r.IsConnected).Returns(false);

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (dbDescriptor != null) services.Remove(dbDescriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        options.UseInMemoryDatabase("HealthCheckRedisDownDb");
                    });

                    var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
                    if (redisDescriptor != null) services.Remove(redisDescriptor);

                    services.AddSingleton<IConnectionMultiplexer>(mockRedis.Object);
                });
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Unhealthy", status);

        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal("Unhealthy", entries.GetProperty("redis").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ReadinessEndpoint_WhenDatabaseIsUnhealthy_Returns503ServiceUnavailable()
    {
        // Arrange: Cấu hình connection string PostgreSQL không hợp lệ để ép lỗi kết nối
        var mockRedis = new Mock<IConnectionMultiplexer>();
        var mockDb = new Mock<IDatabase>();
        mockRedis.Setup(r => r.IsConnected).Returns(true);
        mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(mockDb.Object);
        mockDb.Setup(d => d.PingAsync(It.IsAny<CommandFlags>())).ReturnsAsync(TimeSpan.FromMilliseconds(5));

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureServices(services =>
                {
                    var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                    if (dbDescriptor != null) services.Remove(dbDescriptor);

                    services.AddDbContext<ApplicationDbContext>(options =>
                    {
                        // Port không tồn tại và timeout cực ngắn để giả lập database down
                        options.UseNpgsql("Host=127.0.0.1;Port=59999;Database=fake_db;Username=fake;Password=fake;Timeout=1;Command Timeout=1");
                    });

                    var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
                    if (redisDescriptor != null) services.Remove(redisDescriptor);

                    services.AddSingleton<IConnectionMultiplexer>(mockRedis.Object);
                });
            });

        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.Equal("Unhealthy", status);

        var entries = doc.RootElement.GetProperty("entries");
        Assert.Equal("Unhealthy", entries.GetProperty("postgresql").GetProperty("status").GetString());
    }
}
