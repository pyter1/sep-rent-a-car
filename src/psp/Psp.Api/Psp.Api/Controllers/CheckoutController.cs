using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Psp.Api.Data.Entities;
using Psp.Api.Services;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class CheckoutController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly BankClient _bank;

    public CheckoutController(PspDbContext db, BankClient bank)
    {
        _db = db;
        _bank = bank;
    }

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        // Basic validation
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest("MerchantOrderId is required.");
        if (string.IsNullOrWhiteSpace(request.SuccessUrl)) return BadRequest("SuccessUrl is required.");
        if (string.IsNullOrWhiteSpace(request.FailUrl)) return BadRequest("FailUrl is required.");
        if (string.IsNullOrWhiteSpace(request.ErrorUrl)) return BadRequest("ErrorUrl is required.");

        // 1) Create PSP transaction in DB (persist first, then call Bank)
        var tx = new PspTransaction
        {
            Id = Guid.NewGuid(),
            MerchantOrderId = request.MerchantOrderId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = TransactionStatus.Created, // adjust if your enum uses a different name
            CreatedAtUtc = DateTime.UtcNow,
            SuccessUrl = request.SuccessUrl,
            FailUrl = request.FailUrl,
            ErrorUrl = request.ErrorUrl,
            BankPaymentId = null
        };

        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        // 2) Call Bank to initialize payment session
        var bankReq = new BankInitRequest(
            PspTransactionId: tx.Id,
            Amount: tx.Amount,
            Currency: tx.Currency
        );

        BankInitResponse bankResp;
        try
        {
            bankResp = await _bank.InitPaymentAsync(bankReq, ct);
        }
        catch (HttpRequestException ex)
        {
            // Mark as error in DB (so you have traceability)
            tx.Status = TransactionStatus.Error; // adjust name if needed
            await _db.SaveChangesAsync(ct);

            return StatusCode(StatusCodes.Status502BadGateway, $"Bank init failed: {ex.Message}");
        }

        // 3) Update PSP transaction with Bank payment ID and move status forward
        tx.BankPaymentId = bankResp.PaymentId;
        tx.Status = TransactionStatus.Redirected; // adjust name if needed

        await _db.SaveChangesAsync(ct);

        // 4) Return Bank payment page URL (client/browser redirects here)
        return Ok(new PspInitResponse(tx.Id, bankResp.PaymentUrl));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (tx is null) return NotFound();
        return Ok(tx);
    }
}
