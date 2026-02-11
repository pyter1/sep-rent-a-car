using System.Net.Http.Json;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Common.Observability;
using Psp.Api.Data.Entities;
using System.Text;
using Common.Security;
using Psp.Api.Services;



namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/bank")]
public sealed class BankNotifyController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly MerchantCallbackClient _merchantCallback;

    private PspAuditEvent NewAudit(
        AuditEventType type,
        string result,
        PspTransaction tx,
        string? detailsJson = null)
    {
        return new PspAuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Service = "psp",
            EventType = (int)type,
            Result = result,
            CorrelationId = CorrelationIdMiddleware.Get(HttpContext),

            ActorType = "bank",
            ActorId = null,

            MerchantId = tx.MerchantId,
            MerchantOrderId = tx.MerchantOrderId,

            PspTransactionId = tx.Id,
            BankPaymentId = tx.BankPaymentId,
            Stan = tx.Stan,

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


        var path = Request.Path + Request.QueryString;
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, body);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }



    public BankNotifyController(PspDbContext db, IHttpClientFactory httpFactory, IConfiguration config, MerchantCallbackClient merchantCallback)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
        _merchantCallback = merchantCallback;
    }
    [HttpPost("notify")]
    public async Task<IActionResult> Notify([FromBody] PspBankNotifyRequest request, CancellationToken ct)
    {
        var h = await VerifyHmacAsync("Hmac:PspBankSecret", ct);
        if (!h.Ok) return Unauthorized(new { message = h.Message });

        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == request.PspTransactionId, ct);
        if (tx is null) return NotFound("Unknown PSP transaction.");
        var mappedOk = request.Status is PaymentStatus.Paid or PaymentStatus.Failed or PaymentStatus.Expired;
        _db.AuditEvents.Add(NewAudit(
            AuditEventType.PspBankNotifyReceived,
            mappedOk ? "OK" : "FAIL",
            tx,
            detailsJson: $"{{\"bankStatus\":\"{request.Status}\",\"acquirerTimestampUtc\":\"{request.AcquirerTimestampUtc:O}\"}}"
        ));
        // Always persist BankPaymentId (helpful for reconciliation/debugging)
        tx.BankPaymentId = request.BankPaymentId;

        // If bank returned trace fields, store them (best effort)
        if (!string.IsNullOrWhiteSpace(request.Stan))
            tx.Stan ??= request.Stan;

        // Map Bank status -> PSP status
        var newStatus = request.Status switch
        {
            PaymentStatus.Paid => TransactionStatus.Paid,
            PaymentStatus.Failed => TransactionStatus.Failed,
            PaymentStatus.Expired => TransactionStatus.Failed,
            PaymentStatus.Pending => TransactionStatus.Pending,
            PaymentStatus.Created => TransactionStatus.Pending,
            _ => TransactionStatus.Error
        };

        tx.Status = newStatus;
        tx.CardBrand ??= request.CardBrand;
        tx.PanFirst6 ??= request.PanFirst6;
        tx.PanLast4 ??= request.PanLast4;

        tx.UpdatedAtUtc = DateTime.UtcNow;


        // If already successfully notified merchant, keep idempotent behavior
        if (tx.MerchantNotified)
        {
            await _db.SaveChangesAsync(ct);
            return Ok();
        }

        var callbackUrl = tx.Status switch
        {
            TransactionStatus.Paid => tx.SuccessUrl,
            TransactionStatus.Failed => tx.FailUrl,
            _ => tx.ErrorUrl
        };

        tx.MerchantNotifyAttempts += 1;

        var client = _httpFactory.CreateClient("MerchantCallback");

        try
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.PspMerchantCallbackAttempt,
                "OK",
                tx,
                detailsJson: $"{{\"url\":\"{callbackUrl}\",\"attempt\":{tx.MerchantNotifyAttempts}}}"
            ));
            var resp = await _merchantCallback.PostSignedAsync(
                merchantId: tx.MerchantId,
                callbackUrl: callbackUrl,
                payload: new
                {
                    pspTransactionId = tx.Id,
                    merchantId = tx.MerchantId,
                    merchantOrderId = tx.MerchantOrderId,
                    bankPaymentId = tx.BankPaymentId,
                    stan = tx.Stan,
                    status = tx.Status.ToString(),

                    // forward PCI-safe card metadata from bank notify
                    cardBrand = request.CardBrand,
                    panLast4 = request.PanLast4
                },
                ct: ct
            );
            if (resp.IsSuccessStatusCode)
            {
                _db.AuditEvents.Add(NewAudit(AuditEventType.PspMerchantCallbackSuccess, "OK", tx));
                tx.MerchantNotified = true;
                tx.MerchantNotifiedAtUtc = DateTime.UtcNow;
                tx.MerchantNotifyLastError = null;
            }
            else
            {
                _db.AuditEvents.Add(NewAudit(
                    AuditEventType.PspMerchantCallbackFail,
                    "FAIL",
                    tx,
                    detailsJson: $"{{\"http\":{(int)resp.StatusCode}}}"
                ));
                tx.MerchantNotifyLastError = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            _db.AuditEvents.Add(NewAudit(
                AuditEventType.PspMerchantCallbackFail,
                "FAIL",
                tx,
                detailsJson: "{\"exception\":\"merchant_callback_failed\"}"
            ));
            // Do not throw: bank is finished; record error and allow retry later
            tx.MerchantNotifyLastError = ex.Message;
        }

        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
