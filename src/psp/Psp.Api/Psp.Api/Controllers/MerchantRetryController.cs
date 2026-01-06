using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class MerchantRetryController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public MerchantRetryController(PspDbContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    [HttpPost("{id:guid}/notify-merchant")]
    public async Task<ActionResult> Retry(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound();

        if (tx.MerchantNotified) return Ok(new { message = "Already notified." });

        var callbackUrl = tx.Status switch
        {
            Common.Contracts.TransactionStatus.Paid => tx.SuccessUrl,
            Common.Contracts.TransactionStatus.Failed => tx.FailUrl,
            _ => tx.ErrorUrl
        };

        tx.MerchantNotifyAttempts += 1;

        try
        {
            var client = _httpFactory.CreateClient();
            var resp = await client.PostAsJsonAsync(callbackUrl, new
            {
                pspTransactionId = tx.Id,
                bankPaymentId = tx.BankPaymentId,
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
            tx.MerchantNotifyLastError = ex.Message;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new
        {
            tx.Id,
            tx.Status,
            tx.MerchantNotified,
            tx.MerchantNotifyAttempts,
            tx.MerchantNotifyLastError
        });
    }
}
