using Bank.Api.Data;
using Bank.Api.Data.Entities;
using Bank.Api.Services;
using Common.Contracts;
using Common.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly PspNotifyClient _psp;

    // TTL for "time-limited URL" (tune later)
    private static readonly TimeSpan PaymentTtl = TimeSpan.FromMinutes(5);
    private readonly IConfiguration _config;

    public PaymentsController(BankDbContext db, PspNotifyClient psp, IConfiguration config)
    {
        _db = db;
        _psp = psp;
        _config = config;
    }

    [HttpPost("init")]
    public async Task<ActionResult<BankInitResponse>> Init([FromBody] BankInitRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");

        var now = DateTime.UtcNow;

        var payment = new BankPayment
        {
            Id = Guid.NewGuid(),
            PspTransactionId = request.PspTransactionId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = PaymentStatus.Created,
            Attempted = false,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(PaymentTtl)
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        // URL the browser would open (UI later)
        // var paymentUrl = $"http://localhost:7002/payments/{payment.Id}";
        // var uiBase = HttpContext.RequestServices
        //     .GetRequiredService<IConfiguration>()["Ui:PublicBaseUrl"]
        //     ?? "http://localhost:4202";

        // var paymentUrl = $"{uiBase}/payments/{payment.Id}";
        var uiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4202";
        var paymentUrl = $"{uiBase}/payments/{payment.Id}";


        return Ok(new BankInitResponse(payment.Id, paymentUrl));
    }

[HttpGet("{paymentId:guid}")]
public async Task<ActionResult<object>> GetStatus(Guid paymentId, CancellationToken ct)
{
    var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
    if (p is null) return NotFound();

    var now = DateTime.UtcNow;

    // 1) Transition: Created -> Expired
    if (p.Status == PaymentStatus.Created && now > p.ExpiresAtUtc)
    {
        p.Status = PaymentStatus.Expired;
        // Attempted remains false
    }

    // 2) Side effect: notify PSP exactly once
// Notify PSP for Expired exactly once (per-status idempotency)
    if (p.Status == PaymentStatus.Expired && p.NotifiedPspStatus != PaymentStatus.Expired)
    {
        try
        {
            await _psp.NotifyAsync(
                new PspBankNotifyRequest(p.PspTransactionId, p.Id, p.Status),
                ct
            );

            // Mark which status was successfully notified
            p.NotifiedPspStatus = PaymentStatus.Expired;
        }
        catch
        {
            // PSP is down; keep NotifiedPspStatus unchanged so you can retry later
        }
    }

    await _db.SaveChangesAsync(ct);

    return Ok(new
    {
        paymentId = p.Id,
        pspTransactionId = p.PspTransactionId,
        amount = p.Amount,
        currency = p.Currency,
        status = p.Status,
        attempted = p.Attempted,
        expiresAtUtc = p.ExpiresAtUtc,
        notifiedPspStatus = p.NotifiedPspStatus

    });
}


    [HttpPost("{paymentId:guid}/card/submit")]
    public async Task<ActionResult<object>> SubmitCard(Guid paymentId, [FromBody] CardSubmitRequest request, CancellationToken ct)
    {
        var s = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (s is null) return NotFound();

        // Expiry enforcement
        if (s.Status == PaymentStatus.Created && DateTime.UtcNow > s.ExpiresAtUtc)
        {
            s.Status = PaymentStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return StatusCode(StatusCodes.Status410Gone, new { message = "Payment session expired." });
        }

        // One-time submit enforcement
        if (s.Attempted) return Conflict(new { message = "Payment already attempted." });

        // Validate input (PCI: never store CVV; only validate)
        if (!Luhn.IsValid(request.Pan)) return BadRequest(new { message = "Invalid PAN (Luhn failed)." });
        if (!ExpiryValidator.IsValidNotExpired(request.ExpiryMonth, request.ExpiryYear)) return BadRequest(new { message = "Invalid/expired card date." });
        if (string.IsNullOrWhiteSpace(request.Cvv) || request.Cvv.Length < 3 || request.Cvv.Length > 4 || !request.Cvv.All(char.IsDigit))
            return BadRequest(new { message = "Invalid CVV." });

        // Simulate auth success
        s.Attempted = true;
        s.Status = PaymentStatus.Paid;
        await _db.SaveChangesAsync(ct);

        // Notify PSP
    // Notify PSP for Paid exactly once (per-status idempotency)
    if (s.NotifiedPspStatus != PaymentStatus.Paid)
    {
        try
        {
            await _psp.NotifyAsync(
                new PspBankNotifyRequest(s.PspTransactionId, s.Id, s.Status),
                ct
            );

            s.NotifiedPspStatus = PaymentStatus.Paid;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // PSP down; keep NotifiedPspStatus unchanged so you can retry later
        }
    }


        return Ok(new { message = "Payment completed.", status = s.Status });
    }

    [HttpPost("{paymentId:guid}/qr/confirm")]
    public async Task<ActionResult<object>> ConfirmQr(Guid paymentId, CancellationToken ct)
    {
        var s = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (s is null) return NotFound();

        if (s.Status == PaymentStatus.Created && DateTime.UtcNow > s.ExpiresAtUtc)
        {
            s.Status = PaymentStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return StatusCode(StatusCodes.Status410Gone, new { message = "Payment session expired." });
        }

        if (s.Attempted) return Conflict(new { message = "Payment already attempted." });

        s.Attempted = true;
        s.Status = PaymentStatus.Paid;
        await _db.SaveChangesAsync(ct);

    if (s.NotifiedPspStatus != PaymentStatus.Paid)
    {
        try
        {
            await _psp.NotifyAsync(new PspBankNotifyRequest(s.PspTransactionId, s.Id, s.Status), ct);
            s.NotifiedPspStatus = PaymentStatus.Paid;
            await _db.SaveChangesAsync(ct);
        }
        catch { }
    }


        return Ok(new { message = "QR payment confirmed.", status = s.Status });
    }
}
