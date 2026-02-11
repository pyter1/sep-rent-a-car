using Bank.Api.Data;
using Bank.Api.Data.Entities;
using Bank.Api.Services;
using Common.Contracts;
using Common.Observability;
using Common.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Common.Security;
using System.Text;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly PspNotifyClient _psp;
    private readonly IConfiguration _config;

    private static readonly TimeSpan PaymentTtl = TimeSpan.FromMinutes(5);

    private readonly IpsQrService _ips;

    public PaymentsController(BankDbContext db, PspNotifyClient psp, IConfiguration config, IpsQrService ips)
    {
        _db = db;
        _psp = psp;
        _config = config;
        _ips = ips;
    }

    private BankAuditEvent NewAudit(
        AuditEventType type,
        string result,
        Guid? paymentId = null,
        Guid? pspTxId = null,
        string? merchantId = null,
        string? merchantOrderId = null,
        string? stan = null,
        string? detailsJson = null)
    {
        return new BankAuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Service = "bank",
            EventType = (int)type,
            Result = result,
            CorrelationId = CorrelationIdMiddleware.Get(HttpContext),

            ActorType = "anonymous",
            ActorId = null,

            MerchantId = merchantId,
            MerchantOrderId = merchantOrderId,

            BankPaymentId = paymentId,
            PspTransactionId = pspTxId,
            Stan = stan,

            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            DetailsJson = detailsJson
        };
    }
        private static string GetLast4(string pan)
    {
        var digits = new string(pan.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : digits;
    }

    private static string DetectBrand(string pan)
    {
        var digits = new string(pan.Where(char.IsDigit).ToArray());
        if (digits.Length < 2) return "UNKNOWN";

        // Visa: starts with 4, length 13/16/19
        if (digits[0] == '4') return "VISA";

        // Mastercard: 51-55 or 2221-2720
        if (digits.Length >= 2)
        {
            var first2 = int.Parse(digits.Substring(0, 2));
            if (first2 is >= 51 and <= 55) return "MASTERCARD";
        }
        if (digits.Length >= 4)
        {
            var first4 = int.Parse(digits.Substring(0, 4));
            if (first4 is >= 2221 and <= 2720) return "MASTERCARD";
        }

        // Amex: 34 or 37
        if (digits.StartsWith("34") || digits.StartsWith("37")) return "AMEX";

        // Discover (optional)
        if (digits.StartsWith("6011") || digits.StartsWith("65")) return "DISCOVER";

        return "UNKNOWN";
    }


    // Step 4 (optional) - enable after Common.Security is included + secrets configured
    
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

        var path = Request.Path + Request.QueryString;
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, body);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }
    private string BuildPaymentUrl(Guid paymentId)
    {
        var uiBase = _config["Ui:PublicBaseUrl"] ?? "http://localhost:4202";
        var secret = _config["PaymentToken:Secret"]
            ?? throw new InvalidOperationException("PaymentToken:Secret missing.");

        var token = Bank.Api.Security.PaymentToken.Create(paymentId, secret);
        return $"{uiBase}/payments/{paymentId}?t={Uri.EscapeDataString(token)}";
    }

    private bool RequirePaymentToken(Guid paymentId)
    {
        var secret = _config["PaymentToken:Secret"];
        if (string.IsNullOrWhiteSpace(secret)) return false;

        var token = Request.Headers["X-Payment-Token"].ToString();

        if (string.IsNullOrWhiteSpace(token))
            token = Request.Query["t"].ToString(); // fallback for direct calls / debugging

        return Bank.Api.Security.PaymentToken.Validate(paymentId, secret, token);
    }


    [HttpPost("init")]
    public async Task<ActionResult<BankInitResponse>> Init([FromBody] BankInitRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.MerchantId)) return BadRequest("MerchantId is required.");
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.Stan)) return BadRequest("Stan is required.");
        if (request.PspTimestampUtc == default) return BadRequest("PspTimestampUtc is required.");

        // Step 4 (optional)
        
        var h = await VerifyHmacAsync("Hmac:PspBankSecret", ct);
        if (!h.Ok)
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.SecurityPolicyViolation,
                "FAIL",
                merchantId: request.MerchantId,
                stan: request.Stan,
                detailsJson: $"{{\"reason\":\"hmac_failed\",\"message\":\"{h.Message}\"}}"
            ));
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { message = h.Message });
        }
        

        var expectedPspMerchantId = _config["Psp:MerchantId"] ?? "PSP_ACQUIRER_MERCHANT_ID";
        if (!string.Equals(request.MerchantId.Trim(), expectedPspMerchantId, StringComparison.Ordinal))
            return Unauthorized(new { message = "Invalid PSP merchant identity for acquirer bank." });

        var now = DateTime.UtcNow;

        var existing = await _db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.PspMerchantId == expectedPspMerchantId &&
                x.Stan == request.Stan &&
                x.PspTimestampUtc == DateTime.SpecifyKind(request.PspTimestampUtc, DateTimeKind.Utc),
                ct);

        if (existing is not null)
        {
            return Ok(new BankInitResponse(existing.Id, BuildPaymentUrl(existing.Id)));
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

        _db.AuditEvents.Add(NewAudit(
            AuditEventType.BankPaymentCreated,
            "OK",
            paymentId: payment.Id,
            pspTxId: payment.PspTransactionId,
            merchantId: payment.PspMerchantId,
            stan: payment.Stan,
            detailsJson: $"{{\"amount\":{payment.Amount},\"currency\":\"{payment.Currency}\",\"pspTimestampUtc\":\"{payment.PspTimestampUtc:O}\"}}"
        ));

        await _db.SaveChangesAsync(ct);
        return Ok(new BankInitResponse(payment.Id, BuildPaymentUrl(payment.Id)));
    }

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

        return Ok(new BankInitResponse(p.Id, BuildPaymentUrl(p.Id)));
    }

    [HttpGet("{paymentId:guid}")]
    public async Task<ActionResult<object>> GetStatus(Guid paymentId, CancellationToken ct)
    {
        if (!RequirePaymentToken(paymentId))
            return Unauthorized(new { message = "Missing/invalid payment token." });
        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null) return NotFound();

        var now = DateTime.UtcNow;
        var oldStatus = p.Status;

        if (p.Status == PaymentStatus.Created && now > p.ExpiresAtUtc)
        {
            p.Status = PaymentStatus.Expired;
        }

        if (oldStatus == PaymentStatus.Created && p.Status == PaymentStatus.Expired)
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.BankPaymentExpired,
                "OK",
                paymentId: p.Id,
                pspTxId: p.PspTransactionId,
                merchantId: p.PspMerchantId,
                stan: p.Stan,
                detailsJson: $"{{\"expiresAtUtc\":\"{p.ExpiresAtUtc:O}\"}}"
            ));
        }

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
                        AcquirerTimestampUtc: DateTime.UtcNow,
                        CardBrand: p.CardBrand,
                        PanFirst6: p.PanFirst6,
                        PanLast4: p.PanLast4
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
            // paymentMethod = p.PaymentMethod,
            attempted = p.Attempted,
            expiresAtUtc = p.ExpiresAtUtc,
            notifiedPspStatus = p.NotifiedPspStatus,

            pspMerchantId = p.PspMerchantId,
            stan = p.Stan,
            pspTimestampUtc = p.PspTimestampUtc
        });
    }

    [HttpGet("{paymentId:guid}/qr/payload")]
    public async Task<ActionResult<object>> GetIpsQrPayload(Guid paymentId, CancellationToken ct)
    {
        if (!RequirePaymentToken(paymentId))
            return Unauthorized(new { message = "Missing/invalid payment token." });

        var p = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null) return NotFound(new { message = "Payment not found." });

        var built = _ips.BuildForPayment(p);
        if (!built.Ok)
            return BadRequest(new { message = "Cannot build IPS QR payload.", errors = built.Errors });

        return Ok(new { payload = built.Payload });
    }


    [HttpPost("{paymentId:guid}/card/submit")]
    public async Task<ActionResult<object>> SubmitCard(Guid paymentId, [FromBody] CardSubmitRequest request, CancellationToken ct)
    {
        if (!RequirePaymentToken(paymentId))
            return Unauthorized(new { message = "Missing/invalid payment token." });

        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (p is null) return NotFound();

        async Task<ActionResult<object>> Fail(string message, string code)
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.BankCardSubmit,
                "FAIL",
                paymentId: p.Id,
                pspTxId: p.PspTransactionId,
                merchantId: p.PspMerchantId,
                stan: p.Stan,
                detailsJson: $"{{\"code\":\"{code}\"}}"
            ));
            await _db.SaveChangesAsync(ct);
            return BadRequest(new { message });
        }

        if (p.Attempted) return Conflict(new { message = "Payment session already used (one-time URL)." });

        if (DateTime.UtcNow > p.ExpiresAtUtc)
        {
            if (p.Status != PaymentStatus.Expired)
            {
                p.Status = PaymentStatus.Expired;
                _db.AuditEvents.Add(NewAudit(
                    AuditEventType.BankPaymentExpired,
                    "OK",
                    paymentId: p.Id,
                    pspTxId: p.PspTransactionId,
                    merchantId: p.PspMerchantId,
                    stan: p.Stan,
                    detailsJson: $"{{\"expiresAtUtc\":\"{p.ExpiresAtUtc:O}\"}}"
                ));
                await _db.SaveChangesAsync(ct);
            }
            return await Fail("Payment session expired.", "expired");
        }

        if (!Luhn.IsValid(request.Pan)) return await Fail("Invalid PAN (Luhn failed).", "invalid_pan");
        p.CardBrand = DetectBrand(request.Pan);
        p.PanLast4 = GetLast4(request.Pan); 
        if (!ExpiryValidator.IsValidNotExpired(request.ExpiryMonth, request.ExpiryYear))
            return await Fail("Invalid/expired card date.", "invalid_expiry");

        if (string.IsNullOrWhiteSpace(request.Cvv)
            || request.Cvv.Length < 3
            || request.Cvv.Length > 4
            || !request.Cvv.All(char.IsDigit))
            return await Fail("Invalid CVV.", "invalid_cvv");

        p.Attempted = true;
        p.Status = PaymentStatus.Paid;

        _db.AuditEvents.Add(NewAudit(
            AuditEventType.BankCardSubmit,
            "OK",
            paymentId: p.Id,
            pspTxId: p.PspTransactionId,
            merchantId: p.PspMerchantId,
            stan: p.Stan,
            detailsJson: "{\"decision\":\"paid\"}"
        ));

        await _db.SaveChangesAsync(ct);

        try
        {
            await _psp.NotifyAsync(new PspBankNotifyRequest(
                PspTransactionId: p.PspTransactionId,
                BankPaymentId: p.Id,
                Status: p.Status,
                Stan: p.Stan,
                AcquirerTimestampUtc: DateTime.UtcNow,
                CardBrand: p.CardBrand,
                PanLast4: p.PanLast4
            ), ct);

            p.NotifiedPspStatus = p.Status;
            await _db.SaveChangesAsync(ct);
        }
        catch { }

        return Ok(new { message = "Card submitted.", status = p.Status });
    }

    [HttpPost("{paymentId:guid}/qr/confirm")]
    public async Task<ActionResult<object>> ConfirmQr(Guid paymentId, CancellationToken ct)
    {
        if (!RequirePaymentToken(paymentId))
            return Unauthorized(new { message = "Missing/invalid payment token." });

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

        _db.AuditEvents.Add(NewAudit(
            AuditEventType.BankQrConfirm,
            "OK",
            paymentId: s.Id,
            pspTxId: s.PspTransactionId,
            merchantId: s.PspMerchantId,
            stan: s.Stan
        ));

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
                    AcquirerTimestampUtc: DateTime.UtcNow,
                    CardBrand: s.CardBrand,
                    PanLast4: s.PanLast4
                ), ct);

                s.NotifiedPspStatus = s.Status;
                await _db.SaveChangesAsync(ct);
            }
            catch { }
        }

        return Ok(new { message = "QR payment confirmed.", status = s.Status });
    }
}
