using EventTicketing.Api.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

if (!string.IsNullOrEmpty(redisConnection))
{
    redisConnection = redisConnection.Replace("${REDIS_CONNECTION}", Environment.GetEnvironmentVariable("REDIS_CONNECTION"));
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
        StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnection));
}

builder.Services.AddHealthChecks();

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
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapHealthChecks("/health");

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

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
