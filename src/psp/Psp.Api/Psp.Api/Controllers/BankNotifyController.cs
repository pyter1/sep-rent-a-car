using System.Net.Http.Json;
using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/bank")]
public sealed class BankNotifyController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public BankNotifyController(PspDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    [HttpPost("notify")]
    public async Task<IActionResult> Notify([FromBody] PspBankNotifyRequest request, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == request.PspTransactionId, ct);
        if (tx is null) return NotFound("Unknown PSP transaction.");

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
            _ => TransactionStatus.Error
        };

        tx.Status = newStatus;

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
            var resp = await client.PostAsJsonAsync(callbackUrl, new
            {
                pspTransactionId = tx.Id,
                merchantId = tx.MerchantId,
                merchantOrderId = tx.MerchantOrderId,
                bankPaymentId = tx.BankPaymentId,
                stan = tx.Stan,
                status = tx.Status.ToString()
            }, ct);

            if (resp.IsSuccessStatusCode)
            {
                tx.MerchantNotified = true;
                tx.MerchantNotifiedAtUtc = DateTime.UtcNow;
                tx.MerchantNotifyLastError = null;
            }
            else
            {
                tx.MerchantNotifyLastError = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            // Do not throw: bank is finished; record error and allow retry later
            tx.MerchantNotifyLastError = ex.Message;
        }

        await _db.SaveChangesAsync(ct);
        return Ok();
    }
}
