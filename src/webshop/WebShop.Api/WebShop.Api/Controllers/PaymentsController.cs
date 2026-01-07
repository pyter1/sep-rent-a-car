using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        // This must be reachable from PSP (in Docker: http://webshop-api:7003)
        var publicBaseUrl = _config["PublicBaseUrl"] ?? "http://localhost:7003";
        publicBaseUrl = publicBaseUrl.TrimEnd('/');

        // Build a new request but force callback URLs
        var reqForPsp = request with
        {
            SuccessUrl = $"{publicBaseUrl}/payment/success",
            FailUrl    = $"{publicBaseUrl}/payment/fail",
            ErrorUrl   = $"{publicBaseUrl}/payment/error"
        };

        var result = await _psp.InitAsync(reqForPsp, ct);
        return Ok(result);
    }
}
