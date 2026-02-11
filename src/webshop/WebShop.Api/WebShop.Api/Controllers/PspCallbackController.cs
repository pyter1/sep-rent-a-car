using System.Text;
using Common.Observability;
using Common.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebShop.Api.Contracts;
using WebShop.Api.Data;
using WebShop.Api.Data.Entities;
using WebShop.Api.Middleware;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("payment")]
public sealed class PspCallbackController : ControllerBase
{
    private readonly WebShopDbContext _db;
    private readonly IConfiguration _cfg;

    private const int Audit_PspCallbackReceived = 2101;
    private const int Audit_PspCallbackBadSignature = 2102;
    private const int Audit_OrderStatusChanged = 2103;

    public PspCallbackController(WebShopDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    [HttpPost("/payment/success")]
    public async Task<ActionResult> Success([FromBody] PspCallbackPayload payload, CancellationToken ct)
        => await Handle(payload, OrderStatus.Paid, ct);

    [HttpPost("/payment/fail")]
    public async Task<ActionResult> Fail([FromBody] PspCallbackPayload payload, CancellationToken ct)
        => await Handle(payload, OrderStatus.Failed, ct);

    [HttpPost("/payment/error")]
    public async Task<ActionResult> Error([FromBody] PspCallbackPayload payload, CancellationToken ct)
        => await Handle(payload, OrderStatus.Error, ct);

    private async Task<ActionResult> Handle(PspCallbackPayload payload, OrderStatus status, CancellationToken ct)
    {
        var (ok, msg) = await VerifyHmacFromPspAsync(ct);
        if (!ok)
        {
            _db.AuditEvents.Add(NewAudit(
                Audit_PspCallbackBadSignature,
                result: "FAIL",
                merchantId: payload.merchantId,
                merchantOrderId: payload.merchantOrderId,
                pspTxId: payload.pspTransactionId,
                bankPaymentId: payload.bankPaymentId,
                stan: payload.stan,
                detailsJson: $"{{\"reason\":\"{msg}\"}}"
            ));
            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { message = msg });
        }

        _db.AuditEvents.Add(NewAudit(
            Audit_PspCallbackReceived,
            result: "OK",
            merchantId: payload.merchantId,
            merchantOrderId: payload.merchantOrderId,
            pspTxId: payload.pspTransactionId,
            bankPaymentId: payload.bankPaymentId,
            stan: payload.stan,
            detailsJson: $"{{\"endpointStatus\":\"{status}\"}}"
        ));

        if (string.IsNullOrWhiteSpace(payload.merchantOrderId))
            return BadRequest(new { message = "merchantOrderId is required." });

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.MerchantOrderId == payload.merchantOrderId, ct);
        if (order is null)
            return NotFound(new { message = "Unknown merchantOrderId." });

        // idempotency: never downgrade Paid
        if (order.Status == OrderStatus.Paid)
            return Ok(new { received = "already_paid" });

        var old = order.Status;

        order.PspTransactionId = payload.pspTransactionId;
        order.BankPaymentId = payload.bankPaymentId;
        order.Stan = payload.stan;

        // persist PCI-safe card metadata for order history
        order.CardBrand = payload.cardBrand;
        order.PanLast4 = payload.panLast4;

        order.Status = status;
        order.UpdatedAtUtc = DateTime.UtcNow;
        if (status == OrderStatus.Paid) order.PaidAtUtc = DateTime.UtcNow;

        _db.AuditEvents.Add(NewAudit(
            Audit_OrderStatusChanged,
            result: "OK",
            merchantId: payload.merchantId,
            merchantOrderId: payload.merchantOrderId,
            pspTxId: payload.pspTransactionId,
            bankPaymentId: payload.bankPaymentId,
            stan: payload.stan,
            detailsJson: $"{{\"from\":\"{old}\",\"to\":\"{status}\"}}"
        ));

        await _db.SaveChangesAsync(ct);

        return Ok(new { received = status.ToString(), merchantOrderId = order.MerchantOrderId });
    }

    private WebShopAuditEvent NewAudit(
        int eventType,
        string result,
        string? merchantId,
        string? merchantOrderId,
        Guid? pspTxId,
        Guid? bankPaymentId,
        string? stan,
        string? detailsJson)
    {
        return new WebShopAuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Service = "webshop",
            EventType = eventType,
            Result = result,
            CorrelationId = CorrelationIdMiddleware.Get(HttpContext),

            ActorType = "psp",
            ActorId = null,

            MerchantId = merchantId,
            MerchantOrderId = merchantOrderId,
            PspTransactionId = pspTxId,
            BankPaymentId = bankPaymentId,
            Stan = stan,

            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            DetailsJson = detailsJson
        };
    }

    private async Task<(bool Ok, string Message)> VerifyHmacFromPspAsync(CancellationToken ct)
    {
        var secret = _cfg["Hmac:WebShopPspSecret"];
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

        var rawBody = HttpContext.Items.TryGetValue(RequestBodyCaptureMiddleware.RawBodyItemKey, out var v)
            ? v?.ToString() ?? ""
            : "";

        var path = Request.Path + Request.QueryString;
        var canonical = HmacSigner.BuildCanonical(tsHeader, Request.Method, path, rawBody);
        var expected = HmacSigner.ComputeSignatureBase64(secret, canonical);

        return (HmacVerifier.ConstantTimeEquals(expected, sigHeader), "Bad signature.");
    }
}
