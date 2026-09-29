using EventTicketing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using StackExchange.Redis;
using EventTicketing.Api.Security;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = [
            new() { Url = "/" }
        ];

        return Task.CompletedTask;
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

if (!string.IsNullOrEmpty(connectionString))
{
    connectionString = connectionString.Replace("${DB_HOST}", Environment.GetEnvironmentVariable("DB_HOST"))
                                       .Replace("${DB_PORT}", Environment.GetEnvironmentVariable("DB_PORT"))
                                       .Replace("${DB_NAME}", Environment.GetEnvironmentVariable("DB_NAME"))
                                       .Replace("${DB_USER}", Environment.GetEnvironmentVariable("DB_USER"))
                                       .Replace("${DB_PASSWORD}", Environment.GetEnvironmentVariable("DB_PASSWORD"));
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"]
                      ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION");

if (string.IsNullOrEmpty(redisConnection) || redisConnection == "${REDIS_CONNECTION}")
{
    redisConnection = "localhost:6379";
}
else
{
    var envRedis = Environment.GetEnvironmentVariable("REDIS_CONNECTION");
    if (!string.IsNullOrEmpty(envRedis))
    {
        redisConnection = redisConnection.Replace("${REDIS_CONNECTION}", envRedis);
    }
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnection));

builder.Services.AddHealthChecks();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, CustomAuthorizationMiddlewareResultHandler>();
var app = builder.Build();

// Automatically apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapPost("/api/auth/login", async (
    [Microsoft.AspNetCore.Mvc.FromBody] LoginRequest request, 
    [Microsoft.AspNetCore.Mvc.FromServices] AppDbContext db, 
    [Microsoft.AspNetCore.Mvc.FromServices] IConnectionMultiplexer redis) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password))
    {
        return Results.Json(new { message = "Invalid email or password" }, statusCode: 401);
    }

    var redisDb = redis.GetDatabase();
    var emailLower = request.Email.ToLowerInvariant();
    var lockKey = $"lockout:{emailLower}";
    var attemptsKey = $"attempts:{emailLower}";

    // Check lockout status
    var lockTimeRemaining = await redisDb.KeyTimeToLiveAsync(lockKey);
    if (lockTimeRemaining.HasValue && lockTimeRemaining.Value.TotalSeconds > 0)
    {
        return Results.Json(new { message = "Account locked for 15 minutes" }, statusCode: 403);
    }

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
    if (user == null)
    {
        return Results.Json(new { message = "Invalid email or password" }, statusCode: 401);
    }

    if (!PasswordHasher.VerifyPassword(user.PasswordHash, request.Password))
    {
        var attempts = await redisDb.StringIncrementAsync(attemptsKey);
        if (attempts == 1)
        {
            await redisDb.KeyExpireAsync(attemptsKey, TimeSpan.FromMinutes(15));
        }

        if (attempts >= 5)
        {
            await redisDb.StringSetAsync(lockKey, "locked", TimeSpan.FromMinutes(15));
            await redisDb.KeyDeleteAsync(attemptsKey);
        }

        return Results.Json(new { message = "Invalid email or password" }, statusCode: 401);
    }

    // On successful login, clear attempts
    await redisDb.KeyDeleteAsync(attemptsKey);

    return Results.Ok(new { message = "Login successful", userId = user.Id });
})
.WithName("Login")
.AllowAnonymous();

app.Run();

public record LoginRequest(string Email, string Password);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
