using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TicketBooking.Api.HealthChecks;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task WriteResponse(HttpContext httpContext, HealthReport result)
    {
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            status = result.Status.ToString(),
            totalDuration = result.TotalDuration.ToString(),
            entries = result.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.ToString(),
                    error = entry.Value.Exception?.Message
                }),
            timestamp = DateTimeOffset.UtcNow,
            version = "1.0.0"
        };

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
