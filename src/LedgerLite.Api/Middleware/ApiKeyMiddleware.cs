using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace LedgerLite.Api.Middleware;

/// <summary>
/// Simple API key authentication middleware.
/// In a real system, API keys would be stored hashed in a database with associated client information.
/// For this demo, we use a hard-coded key.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    
    // Demo API keys - in production these would be in a database, hashed
    private static readonly Dictionary<string, string> ApiKeys = new()
    {
        ["demo-key-1"] = "client-demo-1",
        ["admin-key-1"] = "client-admin-1"
    };

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for health checks
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            await WriteUnauthorizedResponse(context, "Missing API key");
            return;
        }

        if (!ApiKeys.TryGetValue(apiKey, out var clientId))
        {
            _logger.LogWarning("Invalid API key attempted: {ApiKey}", apiKey[..Math.Min(8, apiKey.Length)]);
            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // Store client ID for use in controllers
        context.Items["ClientId"] = clientId;
        context.Items["IsAdmin"] = apiKey.StartsWith("admin-");
        
        _logger.LogDebug("Authenticated client: {ClientId}", clientId);
        
        await _next(context);
    }
    
    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/problem+json";
        
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            Title = "Unauthorized",
            Status = 401,
            Detail = message,
            Instance = context.Request.Path
        };
        
        await context.Response.WriteAsJsonAsync(problem);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}