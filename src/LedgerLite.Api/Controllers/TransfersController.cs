using LedgerLite.Api.Models;
using LedgerLite.Api.Services;
using LedgerLite.Domain.Exceptions;
using LedgerLite.Infrastructure.Data;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LedgerLite.Api.Controllers;

[ApiController]
[Route("transfers")]
public class TransfersController : ControllerBase
{
    private readonly TransferService _transferService;
    private readonly LedgerContext _context;
    private readonly ILogger<TransfersController> _logger;

    public TransfersController(
        TransferService transferService,
        LedgerContext context,
        ILogger<TransfersController> logger)
    {
        _transferService = transferService;
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TransferResponse>> CreateTransfer([FromBody] CreateTransferRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var idempotencyKey = HttpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Missing Idempotency Key",
                Status = 400,
                Detail = "Idempotency-Key header is required for money-moving operations",
                Instance = HttpContext.Request.Path
            });
        }

        var clientId = (string)HttpContext.Items["ClientId"]!;

        try
        {
            var transfer = await _transferService.ExecuteTransferAsync(
                clientId,
                idempotencyKey,
                request.FromAccountId,
                request.ToAccountId,
                request.Amount);

            return Ok(TransferResponse.FromTransfer(transfer));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Resource not found",
                Status = 404,
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InsufficientFundsException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Insufficient funds",
                Status = 400,
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cross-currency"))
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Invalid transfer",
                Status = 400,
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during transfer creation");

            return StatusCode(500, new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Title = "Internal server error",
                Status = 500,
                Detail = "An error occurred while processing the transfer",
                Instance = HttpContext.Request.Path
            });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransferResponse>> GetTransfer(Guid id)
    {
        var transfer = await _context.Transfers.FindAsync(id);

        if (transfer == null)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Title = "Transfer not found",
                Status = 404,
                Detail = $"Transfer with ID {id} was not found",
                Instance = HttpContext.Request.Path
            });
        }

        return TransferResponse.FromTransfer(transfer);
    }
}