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
        // Table 2 validation
        if (string.IsNullOrWhiteSpace(request.MerchantId)) return BadRequest("MerchantId is required.");
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.Stan)) return BadRequest("Stan is required.");
        if (request.PspTimestampUtc == default) return BadRequest("PspTimestampUtc is required.");

        // Validate that the caller is the PSP instance we trust (pre-shared merchant id)
        var expectedPspMerchantId = _config["Psp:MerchantId"] ?? "PSP_ACQUIRER_MERCHANT_ID";
        if (!string.Equals(request.MerchantId.Trim(), expectedPspMerchantId, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid PSP merchant identity for acquirer bank." });

        var now = DateTime.UtcNow;

        // Idempotency: if PSP retries because it didn't receive our response,
        // return the existing PAYMENT_ID/PAYMENT_URL for the same (merchantId, stan, pspTimestamp).
        var existing = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.PspMerchantId == expectedPspMerchantId &&
                x.Stan == request.Stan &&
                x.PspTimestampUtc == DateTime.SpecifyKind(request.PspTimestampUtc, DateTimeKind.Utc),
                ct);

        if (existing is not null)
        {
            var uiBaseExisting = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4202";
            var paymentUrlExisting = $"{uiBaseExisting}/payments/{existing.Id}";
            return Ok(new BankInitResponse(existing.Id, paymentUrlExisting));
        }

        var payment = new BankPayment
        {
            Id = Guid.NewGuid(),
            PspTransactionId = request.PspTransactionId,

            PspMerchantId = expectedPspMerchantId,
            Stan = request.Stan.Trim(),
            PspTimestampUtc = DateTime.SpecifyKind(request.PspTimestampUtc, DateTimeKind.Utc),

            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = PaymentStatus.Created,
            Attempted = false,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(PaymentTtl)
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);

        var uiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4202";
        var paymentUrl = $"{uiBase}/payments/{payment.Id}";

        return Ok(new BankInitResponse(payment.Id, paymentUrl));
    }

    // Spec support: lookup by (MERCHANT_ID + STAN + PSP_TIMESTAMP)
    // Useful when PSP didn't receive init response and does not have PAYMENT_ID.
    [HttpGet("by-trace")]
    public async Task<ActionResult<BankInitResponse>> GetByTrace(
        [FromQuery] string merchantId,
        [FromQuery] string stan,
        [FromQuery] DateTime pspTimestampUtc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(merchantId)) return BadRequest("merchantId is required.");
        if (string.IsNullOrWhiteSpace(stan)) return BadRequest("stan is required.");
        if (pspTimestampUtc == default) return BadRequest("pspTimestampUtc is required.");

        var expectedPspMerchantId = _config["Psp:MerchantId"] ?? "PSP_ACQUIRER_MERCHANT_ID";
        if (!string.Equals(merchantId.Trim(), expectedPspMerchantId, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid PSP merchant identity for acquirer bank." });

        var ts = DateTime.SpecifyKind(pspTimestampUtc, DateTimeKind.Utc);

        var p = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x =>
            x.PspMerchantId == expectedPspMerchantId &&
            x.Stan == stan &&
            x.PspTimestampUtc == ts, ct);

        if (p is null) return NotFound();

        var uiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4202";
        var paymentUrl = $"{uiBase}/payments/{p.Id}";
        return Ok(new BankInitResponse(p.Id, paymentUrl));
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

        // 2) If status changed to final and PSP hasn't been notified, try notify (best effort)
        if (p.Status is PaymentStatus.Paid or PaymentStatus.Failed or PaymentStatus.Expired)
        {
            if (p.NotifiedPspStatus != p.Status)
            {
                try
                {
                    await _psp.NotifyAsync(new PspBankNotifyRequest(
                        PspTransactionId: p.PspTransactionId,
                        BankPaymentId: p.Id,
                        Status: p.Status,
                        Stan: p.Stan,
                        AcquirerTimestampUtc: DateTime.UtcNow
                    ), ct);

                    p.NotifiedPspStatus = p.Status;
                }
                catch
                {
                    // swallow; PSP can retry later
                }
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
            notifiedPspStatus = p.NotifiedPspStatus,

            // Table 2 trace (for debugging/reconciliation)
            pspMerchantId = p.PspMerchantId,
            stan = p.Stan,
            pspTimestampUtc = p.PspTimestampUtc
        });
    }

    [HttpPost("{paymentId:guid}/card/submit")]
    public async Task<ActionResult<object>> SubmitCard(Guid paymentId, [FromBody] CardSubmitRequest request, CancellationToken ct)
    {
        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null) return NotFound();

        // One-time attempt guard (spec: one-time, time-limited URL)
        if (p.Attempted) return Conflict(new { message = "Payment session already used (one-time URL)." });

        // TTL guard
        if (DateTime.UtcNow > p.ExpiresAtUtc)
        {
            p.Status = PaymentStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return BadRequest(new { message = "Payment session expired." });
        }

        // Validate input (PCI: never store CVV; only validate)
        if (!Luhn.IsValid(request.Pan)) return BadRequest(new { message = "Invalid PAN (Luhn failed)." });

        if (!ExpiryValidator.IsValidNotExpired(request.ExpiryMonth, request.ExpiryYear))
            return BadRequest(new { message = "Invalid/expired card date." });

        if (string.IsNullOrWhiteSpace(request.Cvv)
            || request.Cvv.Length < 3
            || request.Cvv.Length > 4
            || !request.Cvv.All(char.IsDigit))
            return BadRequest(new { message = "Invalid CVV." });

        // Mark attempt and simulate success/failure
        p.Attempted = true;
        p.Status = PaymentStatus.Paid; // change to Failed based on test conditions if needed

        await _db.SaveChangesAsync(ct);

        // Notify PSP (best effort)
        try
        {
            await _psp.NotifyAsync(new PspBankNotifyRequest(
                PspTransactionId: p.PspTransactionId,
                BankPaymentId: p.Id,
                Status: p.Status,
                Stan: p.Stan,
                AcquirerTimestampUtc: DateTime.UtcNow
            ), ct);

            p.NotifiedPspStatus = p.Status;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // swallow; PSP can retry later
        }

        return Ok(new { message = "Card submitted.", status = p.Status });
    }

    // Placeholder QR confirmation endpoint (KT2: replace with real QR scanning/IPS flow)
    [HttpPost("{paymentId:guid}/qr/confirm")]
    public async Task<ActionResult<object>> ConfirmQr(Guid paymentId, CancellationToken ct)
    {
        var s = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (s is null) return NotFound();
        if (s.Attempted) return Conflict(new { message = "Payment session already used." });

        if (DateTime.UtcNow > s.ExpiresAtUtc)
        {
            s.Status = PaymentStatus.Expired;
            await _db.SaveChangesAsync(ct);
            return BadRequest(new { message = "Payment session expired." });
        }

        s.Attempted = true;
        s.Status = PaymentStatus.Paid;
        await _db.SaveChangesAsync(ct);

        if (s.NotifiedPspStatus != s.Status)
        {
            try
            {
                await _psp.NotifyAsync(new PspBankNotifyRequest(
                    PspTransactionId: s.PspTransactionId,
                    BankPaymentId: s.Id,
                    Status: s.Status,
                    Stan: s.Stan,
                    AcquirerTimestampUtc: DateTime.UtcNow
                ), ct);

                s.NotifiedPspStatus = s.Status;
                await _db.SaveChangesAsync(ct);
            }
            catch { }
        }

        return Ok(new { message = "QR payment confirmed.", status = s.Status });
    }
}
