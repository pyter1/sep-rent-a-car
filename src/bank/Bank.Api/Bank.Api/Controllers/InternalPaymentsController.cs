using System.Text;
using Bank.Api.Data;
using Common.Contracts;
using Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/internal/payments")]
public sealed class InternalPaymentsController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly IConfiguration _config;

    public InternalPaymentsController(BankDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    private async Task<(bool Ok, string Message)> VerifyHmacAsync(string secretConfigKey, CancellationToken ct)
    {
        var secret = _config[secretConfigKey];
        if (string.IsNullOrWhiteSpace(secret))
            return (false, "HMAC secret not configured on server.");

        var tsHeader = Request.Headers[HmacSigner.TimestampHeader].ToString();
        var sigHeader = Request.Headers[HmacSigner.SignatureHeader].ToString();

        if (!HmacVerifier.TryParseTimestampUtc(tsHeader, out var ts))
            return (false, "Missing/invalid X-TimestampUtc.");

        if (!HmacVerifier.IsFresh(ts, TimeSpan.FromMinutes(5)))
            return (false, "Stale request timestamp.");

        if (string.IsNullOrWhiteSpace(sigHeader))
            return (false, "Missing X-Signature.");

        // For GET, body is empty, but keep the same buffering logic for consistency
        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);

        Request.Body.Position = 0;

        var path = Request.Path + Request.QueryString; // must match client path
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, body);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<ActionResult<BankPaymentStatusResponse>> GetStatus(Guid paymentId, CancellationToken ct)
    {
        // Verify the request is really from PSP
        var h = await VerifyHmacAsync("Hmac:PspBankSecret", ct);
        if (!h.Ok) return Unauthorized(new { message = h.Message });

        var p = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null) return NotFound(new { message = "Unknown payment." });

        // Bank entity does not store method explicitly, so infer it
        var method = (!string.IsNullOrWhiteSpace(p.PanLast4) || !string.IsNullOrWhiteSpace(p.CardBrand))
            ? PaymentMethodType.Card
            : PaymentMethodType.Qr;

        // Return the exact contract your PSP expects (Common.Contracts.BankPaymentStatusResponse)
        return Ok(new BankPaymentStatusResponse(
            PaymentId: p.Id,
            PspTransactionId: p.PspTransactionId,
            Amount: p.Amount,
            Currency: p.Currency,
            Status: p.Status,
            PaymentMethod: method,
            Attempted: p.Attempted,
            ExpiresAtUtc: p.ExpiresAtUtc,
            NotifiedPspStatus: p.NotifiedPspStatus,
            CardBrand: p.CardBrand,
            PanFirst6: p.PanFirst6,
            PanLast4: p.PanLast4,
            PspMerchantId: p.PspMerchantId,
            Stan: p.Stan,
            PspTimestampUtc: p.PspTimestampUtc
        ));
    }
}
