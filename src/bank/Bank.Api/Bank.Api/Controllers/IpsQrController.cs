using Bank.Api.Data;
using Bank.Api.Data.Entities;
using Bank.Api.Services;
using Common.Contracts;
using Common.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bank.Api.Controllers;

[ApiController]
[Route("api/bank/ips")]
public sealed class IpsQrController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly PspNotifyClient _psp;
    private readonly IpsQrService _ips;

    public IpsQrController(BankDbContext db, PspNotifyClient psp, IpsQrService ips)
    {
        _db = db;
        _psp = psp;
        _ips = ips;
    }

    public sealed record ValidateRequest(string Payload);

    [HttpPost("validate")]
    public ActionResult<object> Validate([FromBody] ValidateRequest req)
    {
        var res = _ips.ParseAndValidate(req?.Payload);
        return Ok(new
        {
            ok = res.Ok,
            errors = res.Errors,
            fields = res.Fields,
            embeddedPaymentId = res.Fields?.EmbeddedPaymentId
        });
    }

    public sealed record ConfirmRequest(string Payload);

    // Confirms payment based on decoded QR content (camera scan)
    [HttpPost("confirm")]
    public async Task<ActionResult<object>> Confirm([FromBody] ConfirmRequest req, CancellationToken ct)
    {
        var parsed = _ips.ParseAndValidate(req?.Payload);
        if (!parsed.Ok)
            return BadRequest(new { message = "Invalid IPS QR payload.", errors = parsed.Errors });

        var pid = parsed.Fields?.EmbeddedPaymentId;
        if (pid is null)
            return BadRequest(new { message = "QR does not contain a bank payment token (tag S)." });

        var p = await _db.Payments.FirstOrDefaultAsync(x => x.Id == pid.Value, ct);
        if (p is null) return NotFound(new { message = "Unknown payment." });
        if (p.Attempted) return Conflict(new { message = "Payment session already used." });

        if (DateTime.UtcNow > p.ExpiresAtUtc)
        {
            p.Status = PaymentStatus.Expired;
            _db.AuditEvents.Add(new BankAuditEvent
            {
                TimestampUtc = DateTime.UtcNow,
                Service = "bank",
                EventType = (int)AuditEventType.BankPaymentExpired,
                Result = "FAIL",
                CorrelationId = CorrelationIdMiddleware.Get(HttpContext),
                ActorType = "anonymous",
                MerchantId = p.PspMerchantId,
                BankPaymentId = p.Id,
                PspTransactionId = p.PspTransactionId,
                Stan = p.Stan,
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString(),
                DetailsJson = "{\"source\":\"ips_scan\"}"
            });
            await _db.SaveChangesAsync(ct);
            return BadRequest(new { message = "Payment session expired." });
        }

        p.Attempted = true;
        p.Status = PaymentStatus.Paid;

        _db.AuditEvents.Add(new BankAuditEvent
        {
            TimestampUtc = DateTime.UtcNow,
            Service = "bank",
            EventType = (int)AuditEventType.BankQrConfirm,
            Result = "OK",
            CorrelationId = CorrelationIdMiddleware.Get(HttpContext),
            ActorType = "anonymous",
            MerchantId = p.PspMerchantId,
            BankPaymentId = p.Id,
            PspTransactionId = p.PspTransactionId,
            Stan = p.Stan,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString(),
            DetailsJson = "{\"source\":\"ips_scan\"}"
        });

        await _db.SaveChangesAsync(ct);

        if (p.NotifiedPspStatus != p.Status)
        {
            try
            {
                await _psp.NotifyAsync(new Common.Contracts.PspBankNotifyRequest(
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
        }

        return Ok(new
        {
            message = "QR payment confirmed.",
            status = p.Status,
            bankPaymentId = p.Id,
            pspTransactionId = p.PspTransactionId
        });
    }
}
