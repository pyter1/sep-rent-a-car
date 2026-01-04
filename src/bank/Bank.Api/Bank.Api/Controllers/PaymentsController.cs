using Bank.Api.Services;
using Bank.Api.Storage;
using Common.Contracts;
using Common.Validation;
using Microsoft.AspNetCore.Mvc;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly PaymentSessionStore _store;
    private readonly PspNotifyClient _psp;

    // TTL for "time-limited URL" (tune later)
    private static readonly TimeSpan PaymentTtl = TimeSpan.FromMinutes(5);

    public PaymentsController(PaymentSessionStore store, PspNotifyClient psp)
    {
        _store = store;
        _psp = psp;
    }

    [HttpPost("init")]
    public ActionResult<BankInitResponse> Init([FromBody] BankInitRequest request)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");

        var session = _store.Create(request.PspTransactionId, request.Amount, request.Currency, PaymentTtl);

        // This is the URL the browser would open (UI comes later); for backend-only it's enough.
        var paymentUrl = $"http://localhost:7002/payments/{session.PaymentId}";

        return Ok(new BankInitResponse(session.PaymentId, paymentUrl));
    }

    [HttpGet("{paymentId:guid}")]
    public ActionResult<object> GetStatus(Guid paymentId)
    {
        if (!_store.TryGet(paymentId, out var s) || s is null) return NotFound();

        var now = DateTime.UtcNow;
        if (s.Status == PaymentStatus.Created && now > s.ExpiresAtUtc)
        {
            s = _store.Update(paymentId, cur => cur with { Status = PaymentStatus.Expired });
        }

        return Ok(new
        {
            s.PaymentId,
            s.PspTransactionId,
            s.Amount,
            s.Currency,
            s.Status,
            s.Attempted,
            s.ExpiresAtUtc
        });
    }

    [HttpPost("{paymentId:guid}/card/submit")]
    public async Task<ActionResult<object>> SubmitCard(Guid paymentId, [FromBody] CardSubmitRequest request, CancellationToken ct)
    {
        if (!_store.TryGet(paymentId, out var s) || s is null) return NotFound();

        // Expiry enforcement
        if (s.Status == PaymentStatus.Created && DateTime.UtcNow > s.ExpiresAtUtc)
        {
            s = _store.Update(paymentId, cur => cur with { Status = PaymentStatus.Expired });
            return StatusCode(StatusCodes.Status410Gone, new { message = "Payment session expired." });
        }

        // One-time submit enforcement
        if (s.Attempted) return Conflict(new { message = "Payment already attempted." });

        // Validate input (PCI: never store CVV; only validate)
        if (!Luhn.IsValid(request.Pan)) return BadRequest(new { message = "Invalid PAN (Luhn failed)." });
        if (!ExpiryValidator.IsValidNotExpired(request.ExpiryMonth, request.ExpiryYear)) return BadRequest(new { message = "Invalid/expired card date." });
        if (string.IsNullOrWhiteSpace(request.Cvv) || request.Cvv.Length < 3 || request.Cvv.Length > 4 || !request.Cvv.All(char.IsDigit))
            return BadRequest(new { message = "Invalid CVV." });

        // Simulate authorization success (later you can add fail logic)
        var updated = _store.Update(paymentId, cur => cur with { Attempted = true, Status = PaymentStatus.Paid });

        // Notify PSP
        await _psp.NotifyAsync(new PspBankNotifyRequest(updated.PspTransactionId, updated.PaymentId, updated.Status), ct);

        return Ok(new { message = "Payment completed.", updated.Status });
    }

    [HttpPost("{paymentId:guid}/qr/confirm")]
    public async Task<ActionResult<object>> ConfirmQr(Guid paymentId, CancellationToken ct)
    {
        if (!_store.TryGet(paymentId, out var s) || s is null) return NotFound();

        if (s.Status == PaymentStatus.Created && DateTime.UtcNow > s.ExpiresAtUtc)
        {
            s = _store.Update(paymentId, cur => cur with { Status = PaymentStatus.Expired });
            return StatusCode(StatusCodes.Status410Gone, new { message = "Payment session expired." });
        }

        if (s.Attempted) return Conflict(new { message = "Payment already attempted." });

        var updated = _store.Update(paymentId, cur => cur with { Attempted = true, Status = PaymentStatus.Paid });

        await _psp.NotifyAsync(new PspBankNotifyRequest(updated.PspTransactionId, updated.PaymentId, updated.Status), ct);

        return Ok(new { message = "QR payment confirmed.", updated.Status });
    }
}
