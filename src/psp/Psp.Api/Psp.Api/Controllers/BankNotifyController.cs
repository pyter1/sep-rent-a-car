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
    private readonly HttpClient _http = new(); // simple for now; later use IHttpClientFactory

    public BankNotifyController(PspDbContext db)
    {
        _db = db;
    }

    [HttpPost("notify")]
    public async Task<ActionResult> Notify([FromBody] PspBankNotifyRequest request, CancellationToken ct)
    {
        var tx = await _db.Transactions
            .FirstOrDefaultAsync(x => x.Id == request.PspTransactionId, ct);

        if (tx is null)
            return NotFound("Unknown PSP transaction.");

        // Map Bank status -> PSP transaction status
        var newStatus = request.Status switch
        {
            PaymentStatus.Paid => TransactionStatus.Paid,
            PaymentStatus.Failed => TransactionStatus.Failed,
            PaymentStatus.Expired => TransactionStatus.Failed,
            _ => TransactionStatus.Error
        };

        // Update DB
        tx.Status = newStatus;

        // Keep bank payment id in DB (if you store it)
        if (request.BankPaymentId != Guid.Empty)
            tx.BankPaymentId = request.BankPaymentId;

        await _db.SaveChangesAsync(ct);

        // Select callback URL based on status
        var callbackUrl = newStatus switch
        {
            TransactionStatus.Paid => tx.SuccessUrl,
            TransactionStatus.Failed => tx.FailUrl,
            _ => tx.ErrorUrl
        };

        try
        {
            await _http.PostAsJsonAsync(callbackUrl, new
            {
                pspTransactionId = request.PspTransactionId,
                bankPaymentId = request.BankPaymentId,
                status = newStatus.ToString()
            }, ct);

            return Ok();
        }
        catch
        {
            // Keep it simple: DB is already correct, reconciliation/retry could be added later
            return Ok();
        }
    }
}
