using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LedgerLite.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LedgerLite.Infrastructure.Services;

/// <summary>
/// Handles idempotency records for API operations.
/// Stores request hashes and responses to ensure duplicate requests return identical results.
/// </summary>
public class IdempotencyService
{
    private readonly LedgerContext _context;
    private readonly ILogger<IdempotencyService> _logger;

    public IdempotencyService(LedgerContext context, ILogger<IdempotencyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a request with the given client ID and idempotency key exists.
    /// If it exists with the same request body, returns the stored response.
    /// If it exists with a different request body, throws an exception (422 Unprocessable Entity).
    /// </summary>
    public async Task<IdempotencyResult> CheckIdempotencyAsync<TRequest>(
        string clientId,
        string idempotencyKey,
        TRequest request)
    {
        var requestHash = ComputeRequestHash(request);

        var existingRecord = await _context.Set<IdempotencyRecord>()
            .FirstOrDefaultAsync(r => r.ClientId == clientId && r.Key == idempotencyKey);

        if (existingRecord == null)
        {
            // No existing request, proceed with operation
            return new IdempotencyResult { ShouldProceed = true };
        }

        if (existingRecord.RequestHash != requestHash)
        {
            // Same key, different request body - this is an error
            _logger.LogWarning("Idempotency key conflict: {ClientId}/{Key} - request body differs",
                clientId, idempotencyKey);

            return new IdempotencyResult
            {
                ShouldProceed = false,
                IsConflict = true,
                ConflictMessage = "Same idempotency key used with different request body"
            };
        }

        // Same key, same request body - return stored response
        _logger.LogDebug("Returning cached response for {ClientId}/{Key}", clientId, idempotencyKey);

        return new IdempotencyResult
        {
            ShouldProceed = false,
            IsConflict = false,
            StoredResponse = existingRecord.ResponseBody,
            StoredStatusCode = existingRecord.ResponseStatus
        };
    }

    /// <summary>
    /// Stores the response for a successfully completed idempotent operation.
    /// This should be called within the same transaction as the business operation.
    /// </summary>
    public async Task StoreResponseAsync<TRequest, TResponse>(
        string clientId,
        string idempotencyKey,
        TRequest request,
        TResponse response,
        int statusCode)
    {
        var requestHash = ComputeRequestHash(request);
        var responseBody = JsonSerializer.Serialize(response);

        var record = new IdempotencyRecord
        {
            ClientId = clientId,
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatus = statusCode,
            ResponseBody = responseBody,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<IdempotencyRecord>().Add(record);
        // Note: SaveChanges is called by the caller within their transaction

        _logger.LogDebug("Stored idempotency record for {ClientId}/{Key}", clientId, idempotencyKey);
    }

    private static string ComputeRequestHash<T>(T request)
    {
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

public class IdempotencyResult
{
    public bool ShouldProceed { get; set; }
    public bool IsConflict { get; set; }
    public string? ConflictMessage { get; set; }
    public string? StoredResponse { get; set; }
    public int StoredStatusCode { get; set; }
}

/// <summary>
/// Entity for storing idempotency records in the database.
/// </summary>
public class IdempotencyRecord
{
    public string ClientId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}