using System.Text.Json.Serialization;

using LedgerLite.Api.Middleware;
using LedgerLite.Api.Services;
using LedgerLite.Infrastructure.Extensions;

using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Customize validation error responses to use ProblemDetails
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Validation error",
                Status = 400,
                Instance = context.HttpContext.Request.Path
            };

            return new BadRequestObjectResult(problemDetails);
        };
    })
    .AddJsonOptions(options =>
    {
        // Configure JSON serialization
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Use PascalCase
    });

// Add problem details support
builder.Services.AddProblemDetails();

// Add Infrastructure layer
builder.Services.AddInfrastructure(builder.Configuration);

// Add application services
builder.Services.AddScoped<TransferService>();

// Add logging
builder.Services.AddLogging();

var app = builder.Build();

// Configure request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();

// Add API key authentication
app.UseApiKeyAuthentication();

app.MapControllers();

// Add health check endpoints
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("LivenessCheck");

app.MapGet("/health/ready", async (LedgerLite.Infrastructure.Data.LedgerContext context) =>
{
    try
    {
        // Check database connectivity
        await context.Database.CanConnectAsync();
        return Results.Ok(new { status = "ready", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "not_ready", error = ex.Message, timestamp = DateTime.UtcNow },
            statusCode: 503);
    }
}).WithName("ReadinessCheck");

app.Run();

public partial class Program { } // Make accessible for integration tests