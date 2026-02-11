using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Psp.Api.Services;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class MerchantRetryController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly MerchantCallbackClient _merchantCallback;

    public MerchantRetryController(PspDbContext db, MerchantCallbackClient merchantCallback)
    {
        _db = db;
        _merchantCallback = merchantCallback;
    }

    [HttpPost("{id:guid}/notify-merchant")]
    public async Task<IActionResult> NotifyMerchant(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound();

        if (tx.MerchantNotified)
        {
            return Ok(new
            {
                message = "Merchant already notified.",
                tx.Id,
                tx.Status,
                tx.MerchantNotified,
                tx.MerchantNotifyAttempts,
                tx.MerchantNotifyLastError
            });
        }

        var callbackUrl = tx.Status switch
        {
            Common.Contracts.TransactionStatus.Paid => tx.SuccessUrl,
            Common.Contracts.TransactionStatus.Failed => tx.FailUrl,
            _ => tx.ErrorUrl
        };

        tx.MerchantNotifyAttempts += 1;

        try
        {
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
                    status = tx.Status.ToString()
                },
                ct: ct
            );

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
