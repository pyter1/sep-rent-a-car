﻿using System.Security.Cryptography;
using System.Text;
using Common.Contracts;
using Common.Observability;
// using Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Psp.Api.Data.Entities;
using Psp.Api.Services;
using System.Linq;
using System.Collections.Generic;

using Common.Security;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class CheckoutController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly BankClient _bank;
    private readonly IConfiguration _config;
    private readonly MerchantCallbackClient _merchantCallback;
    private readonly CurrencyConverter _fx;

    public CheckoutController(PspDbContext db, BankClient bank, IConfiguration config,
                            MerchantCallbackClient merchantCallback, CurrencyConverter fx)
    {
        _db = db;
        _bank = bank;
        _config = config;
        _merchantCallback = merchantCallback;
        _fx = fx;
    }

    private PspAuditEvent NewAudit(
        AuditEventType type,
        string result,
        PspTransaction? tx = null,
        Guid? bankPaymentId = null,
        string? detailsJson = null)
    {
        return new PspAuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Service = "psp",
            EventType = (int)type,
            Result = result,
            CorrelationId = CorrelationIdMiddleware.Get(HttpContext),

            ActorType = "merchant",
            ActorId = tx?.MerchantId,

            MerchantId = tx?.MerchantId,
            MerchantOrderId = tx?.MerchantOrderId,

            PspTransactionId = tx?.Id,
            BankPaymentId = bankPaymentId ?? tx?.BankPaymentId,
            Stan = tx?.Stan,

            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            DetailsJson = detailsJson
        };
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

        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);

        Request.Body.Position = 0;

        var path = Request.Path + Request.QueryString; // includes query
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, body);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }


    // Step 4 (optional): verify HMAC from WebShop on /init
    /*
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

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        var path = Request.Path + Request.QueryString;
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, body);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }
    */

    public sealed record StartPaymentResponse(Guid BankPaymentId, string RedirectUrl);

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.MerchantId)) return BadRequest("MerchantId is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantPassword)) return BadRequest("MerchantPassword is required.");
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest("MerchantOrderId is required.");
        if (request.MerchantTimestampUtc == default) return BadRequest("MerchantTimestampUtc is required.");
        if (string.IsNullOrWhiteSpace(request.SuccessUrl)) return BadRequest("SuccessUrl is required.");
        if (string.IsNullOrWhiteSpace(request.FailUrl)) return BadRequest("FailUrl is required.");
        if (string.IsNullOrWhiteSpace(request.ErrorUrl)) return BadRequest("ErrorUrl is required.");

        // Step 4 (preferred): use HMAC instead of MerchantPassword in body
        
        var h = await VerifyHmacAsync("Hmac:WebShopPspSecret", ct);
        if (!h.Ok)
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.SecurityPolicyViolation,
                "FAIL",
                detailsJson: $"{{\"reason\":\"hmac_failed\",\"message\":\"{h.Message}\"}}"
            ));
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { message = h.Message });
        }
        

        if (!TryValidateMerchant(request.MerchantId, request.MerchantPassword, out var authError))
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.SecurityPolicyViolation,
                "FAIL",
                detailsJson: "{\"reason\":\"invalid_merchant_credentials\"}"
            ));
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { message = authError });
        }

        var tx = new PspTransaction
        {
            Id = Guid.NewGuid(),

            MerchantId = request.MerchantId.Trim(),
            MerchantOrderId = request.MerchantOrderId.Trim(),
            MerchantTimestampUtc = DateTime.SpecifyKind(request.MerchantTimestampUtc, DateTimeKind.Utc),

            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),

            Status = TransactionStatus.Created,
            CreatedAtUtc = DateTime.UtcNow,

            SuccessUrl = request.SuccessUrl,
            FailUrl = request.FailUrl,
            ErrorUrl = request.ErrorUrl,

            BankPaymentId = null,
            Stan = null,
            PspTimestampUtc = null,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Transactions.Add(tx);

        _db.AuditEvents.Add(NewAudit(
            AuditEventType.PaymentInit,
            "OK",
            tx,
            detailsJson: $"{{\"amount\":{tx.Amount},\"currency\":\"{tx.Currency}\"}}"
        ));

        await _db.SaveChangesAsync(ct);

        var pspUiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4201";
        var checkoutUrl = $"{pspUiBase.TrimEnd('/')}/checkout/{tx.Id}";

        return Ok(new PspInitResponse(tx.Id, checkoutUrl));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PspTransaction>> Get(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return tx is null ? NotFound() : Ok(tx);
    }

    [HttpPost("{id:guid}/start-card")]
    public async Task<ActionResult<StartPaymentResponse>> StartCard(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound();

        if (tx.BankPaymentId is not null)
            return Conflict(new { message = "Bank payment session already created for this transaction." });

        tx.Stan = GenerateStan();
        tx.PspTimestampUtc = DateTime.UtcNow;
        tx.UpdatedAtUtc = DateTime.UtcNow;

        var bankMerchantId = _config["Bank:MerchantId"] ?? "PSP_ACQUIRER_MERCHANT_ID";

        BankInitResponse bankResp;
        try
        {
            await _db.SaveChangesAsync(ct);

            bankResp = await _bank.InitPaymentAsync(
                new BankInitRequest(
                    MerchantId: bankMerchantId,
                    Amount: tx.Amount,
                    Currency: tx.Currency,
                    Stan: tx.Stan,
                    PspTimestampUtc: tx.PspTimestampUtc.Value,
                    PspTransactionId: tx.Id
                ),
                ct
            );
        }
        catch (HttpRequestException ex)
        {
            tx.Status = TransactionStatus.Error;

            _db.AuditEvents.Add(NewAudit(
                AuditEventType.PaymentStartCard,
                "FAIL",
                tx,
                detailsJson: "{\"bankInit\":\"failed\"}"
            ));

            await _db.SaveChangesAsync(ct);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = $"Bank init failed: {ex.Message}" });
        }

        tx.BankPaymentId = bankResp.PaymentId;
        tx.Status = TransactionStatus.Redirected;

        _db.AuditEvents.Add(NewAudit(
            AuditEventType.PaymentStartCard,
            "OK",
            tx,
            bankPaymentId: bankResp.PaymentId,
            detailsJson: "{\"bankInit\":\"ok\"}"
        ));

        await _db.SaveChangesAsync(ct);
        return Ok(new StartPaymentResponse(bankResp.PaymentId, bankResp.PaymentUrl));
    }

    [HttpPost("{id:guid}/start-qr")]
    public async Task<ActionResult<StartPaymentResponse>> StartQr(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound();

        // If not RSD, convert in PSP for IPS QR compliance.
        if (!string.Equals(tx.Currency, "RSD", StringComparison.OrdinalIgnoreCase))
        {
            var oldAmount = tx.Amount;
            var oldCurrency = tx.Currency;

            tx.Amount = _fx.Convert(tx.Amount, tx.Currency, "RSD");
            tx.Currency = "RSD";
            tx.UpdatedAtUtc = DateTime.UtcNow;

            _db.AuditEvents.Add(NewAudit(
                AuditEventType.SecurityPolicyViolation, // or create a dedicated AuditEventType if you prefer
                "OK",
                tx,
                detailsJson: $"{{\"action\":\"fx_convert_for_qr\",\"from\":\"{oldCurrency}\",\"to\":\"RSD\",\"oldAmount\":{oldAmount},\"newAmount\":{tx.Amount}}}"
            ));

            await _db.SaveChangesAsync(ct);
        }

        if (tx.BankPaymentId is not null)
        {
            var bankUiBase = _config["BankUi:PublicBaseUrl"] ?? "http://localhost:4202";
            var existingUrl = $"{bankUiBase.TrimEnd('/')}/payments/{tx.BankPaymentId}?m=qr";
            return Ok(new StartPaymentResponse(tx.BankPaymentId.Value, existingUrl));
        }

        var start = await StartCard(id, ct);
        if (start.Result is not null) return start;

        var body = start.Value!;
        var newRedirect = AppendQuery(body.RedirectUrl, "m", "qr");
        return Ok(new StartPaymentResponse(body.BankPaymentId, newRedirect));
    }



    private static string AppendQuery(string url, string key, string value)
    {
        // Keeps existing query string (ex: ?t=xyz) and adds/overwrites key (m=qr).
        // Works for absolute and relative URLs.
        var hashIndex = url.IndexOf('#');
        var hash = hashIndex >= 0 ? url[hashIndex..] : "";
        var baseUrl = hashIndex >= 0 ? url[..hashIndex] : url;

        var qIndex = baseUrl.IndexOf('?');
        var path = qIndex >= 0 ? baseUrl[..qIndex] : baseUrl;
        var query = qIndex >= 0 ? baseUrl[(qIndex + 1)..] : "";

        var parts = query.Length == 0
            ? new List<string>()
            : query.Split('&', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Remove existing key if present
        parts = parts
            .Where(p => !p.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            .ToList();

        parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");

        var newQuery = string.Join("&", parts);
        return $"{path}?{newQuery}{hash}";
    }


    private bool TryValidateMerchant(string merchantId, string merchantPassword, out string? error)
    {
        var expected = _config[$"Merchants:{merchantId}:Password"]
                       ?? _config[$"Merchants:{merchantId}"];

        if (string.IsNullOrWhiteSpace(expected))
        {
            error = "Unknown merchant.";
            return false;
        }

        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(merchantPassword);

        if (a.Length != b.Length || !CryptographicOperations.FixedTimeEquals(a, b))
        {
            error = "Invalid merchant credentials.";
            return false;
        }

        error = null;
        return true;
    }

    private static string GenerateStan()
    {
        Span<byte> bytes = stackalloc byte[4];
        RandomNumberGenerator.Fill(bytes);
        var value = BitConverter.ToUInt32(bytes) % 1_000_000;
        return value.ToString("D6");
    }
}
