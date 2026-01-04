using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Psp.Api.Storage;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/bank")]
public sealed class BankNotifyController : ControllerBase
{
    private readonly TransactionStore _store;
    private readonly HttpClient _http = new(); // simple for now; later use IHttpClientFactory

    public BankNotifyController(TransactionStore store)
    {
        _store = store;
    }

    [HttpPost("notify")]
    public async Task<ActionResult> Notify([FromBody] PspBankNotifyRequest request, CancellationToken ct)
    {
        if (!_store.TryGet(request.PspTransactionId, out var tx) || tx is null)
            return NotFound("Unknown PSP transaction.");

        // Map Bank status -> PSP transaction status
        var newStatus = request.Status switch
        {
            PaymentStatus.Paid => TransactionStatus.Paid,
            PaymentStatus.Failed => TransactionStatus.Failed,
            PaymentStatus.Expired => TransactionStatus.Failed,
            _ => TransactionStatus.Error
        };

        _store.SetStatus(request.PspTransactionId, newStatus);

        // Call WebShop callback URL based on status
        var callbackUrl = newStatus switch
        {
            TransactionStatus.Paid => tx.SuccessUrl,
            TransactionStatus.Failed => tx.FailUrl,
            _ => tx.ErrorUrl
        };

        try
        {
            // Minimal callback; later you can send more data and add retries.
            var resp = await _http.PostAsJsonAsync(callbackUrl, new
            {
                pspTransactionId = request.PspTransactionId,
                bankPaymentId = request.BankPaymentId,
                status = newStatus.ToString()
            }, ct);

            // Even if callback fails, we keep PSP status updated; later reconciliation handles retries.
            return Ok();
        }
        catch
        {
            return Ok(); // keep it simple for now
        }
    }
}
