using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Psp.Api.Data;
using Psp.Api.Services;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class ReconcileController : ControllerBase
{
    private readonly PspDbContext _db;
    private readonly BankClient _bank;
    private readonly MerchantCallbackClient _merchantCallback;

    public ReconcileController(PspDbContext db, BankClient bank, MerchantCallbackClient merchantCallback)
    {
        _db = db;
        _bank = bank;
        _merchantCallback = merchantCallback;
    }

    [HttpPost("{id:guid}/reconcile")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        var tx = await _db.Transactions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (tx is null) return NotFound(new { message = "Unknown PSP transaction." });

        if (tx.BankPaymentId is null)
            return BadRequest(new { message = "Transaction has no BankPaymentId yet." });

        BankPaymentStatusResponse bank;
        try
        {
            bank = await _bank.GetInternalStatusAsync(tx.BankPaymentId.Value, ct);
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = "Bank internal status unavailable.", error = ex.Message });
        }

        var old = tx.Status;

        // Map bank PaymentStatus -> PSP TransactionStatus
        var mapped = MapBankToPsp(bank.Status, bank.Attempted, bank.ExpiresAtUtc);

        // Fill PCI-safe card metadata and reconciliation identifiers (only if empty)
        tx.Stan ??= bank.Stan;
        tx.CardBrand ??= bank.CardBrand;
        tx.PanFirst6 ??= bank.PanFirst6;
        tx.PanLast4 ??= bank.PanLast4;

        if (tx.Status != mapped)
        {
            tx.Status = mapped;
            tx.UpdatedAtUtc = DateTime.UtcNow;
        }

        // Retry merchant callback if terminal and not yet notified
        if (IsTerminal(tx.Status) && !tx.MerchantNotified)
        {
            var callbackUrl = tx.Status switch
            {
                TransactionStatus.Paid => tx.SuccessUrl,
                TransactionStatus.Failed => tx.FailUrl,
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
                        status = tx.Status.ToString(),

                        // PCI-safe metadata
                        cardBrand = tx.CardBrand,
                        panFirst6 = tx.PanFirst6,
                        panLast4 = tx.PanLast4,

                        // optional but useful to display on the shop side
                        paymentMethod = bank.PaymentMethod.ToString()
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
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            txId = tx.Id,
            oldStatus = old.ToString(),
            newStatus = tx.Status.ToString(),
            bankStatus = bank.Status.ToString(),
            merchantNotified = tx.MerchantNotified,
            notifyAttempts = tx.MerchantNotifyAttempts,
            notifyLastError = tx.MerchantNotifyLastError
        });
    }

    private static TransactionStatus MapBankToPsp(PaymentStatus bankStatus, bool attempted, DateTime expiresAtUtc)
    {
        return bankStatus switch
        {
            PaymentStatus.Paid => TransactionStatus.Paid,
            PaymentStatus.Failed => TransactionStatus.Failed,
            PaymentStatus.Expired => TransactionStatus.Failed,
            PaymentStatus.Pending => DateTime.UtcNow > expiresAtUtc ? TransactionStatus.Failed : TransactionStatus.Pending,
            _ => attempted ? TransactionStatus.Failed : TransactionStatus.Pending
        };
    }

    private static bool IsTerminal(TransactionStatus s)
        => s == TransactionStatus.Paid || s == TransactionStatus.Failed;
}
