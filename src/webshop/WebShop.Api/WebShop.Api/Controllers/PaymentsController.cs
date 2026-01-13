using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using WebShop.Api.Contracts;
using WebShop.Api.Services;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly PspClient _psp;
    private readonly IConfiguration _config;

    public PaymentsController(PspClient psp, IConfiguration config)
    {
        _psp = psp;
        _config = config;
    }

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] WebShopInitRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return BadRequest(new { message = "Amount must be > 0." });
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest(new { message = "Currency is required." });
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest(new { message = "MerchantOrderId is required." });

        var merchantId = _config["Psp:MerchantId"];
        var merchantPassword = _config["Psp:MerchantPassword"];

        if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantPassword))
            return StatusCode(500, new { message = "Missing Psp:MerchantId / Psp:MerchantPassword in WebShop.Api config." });

        var publicBaseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:7003").TrimEnd('/');

        var reqForPsp = new PspInitRequest(
            MerchantId: merchantId,
            MerchantPassword: merchantPassword,
            Amount: request.Amount,
            Currency: request.Currency.Trim().ToUpperInvariant(),
            MerchantOrderId: request.MerchantOrderId.Trim(),
            MerchantTimestampUtc: DateTime.UtcNow,
            SuccessUrl: $"{publicBaseUrl}/payment/success",
            FailUrl:    $"{publicBaseUrl}/payment/fail",
            ErrorUrl:   $"{publicBaseUrl}/payment/error"
        );

        var result = await _psp.InitAsync(reqForPsp, ct);
        return Ok(result);
    }
}
