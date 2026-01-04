using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using Psp.Api.Services;
using Psp.Api.Storage;

namespace Psp.Api.Controllers;

[ApiController]
[Route("api/psp/transactions")]
public sealed class CheckoutController : ControllerBase
{
    private readonly TransactionStore _store;
    private readonly BankClient _bank;

    public CheckoutController(TransactionStore store, BankClient bank)
    {
        _store = store;
        _bank = bank;
    }

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return BadRequest("Amount must be > 0.");
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest("Currency is required.");
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest("MerchantOrderId is required.");

        var tx = _store.Create(request);

        // Call Bank to initialize payment session
        var bankReq = new BankInitRequest(
            PspTransactionId: tx.TransactionId,
            Amount: tx.Amount,
            Currency: tx.Currency
        );

        BankInitResponse bankResp;
        try
        {
            bankResp = await _bank.InitPaymentAsync(bankReq, ct);
             _store.SetBankPayment(tx.TransactionId, bankResp.PaymentId);
        }
        catch (HttpRequestException ex)
        {
            // Later you’ll improve this with retries + reconciliation.
            return StatusCode(StatusCodes.Status502BadGateway, $"Bank init failed: {ex.Message}");
        }

        // Return Bank payment page URL as the redirectUrl
        return Ok(new PspInitResponse(tx.TransactionId, bankResp.PaymentUrl));
    }

    [HttpGet("{id:guid}")]
    public ActionResult<object> Get(Guid id)
    {
        if (!_store.TryGet(id, out var tx) || tx is null) return NotFound();
        return Ok(tx);
    }
}
