using Common.Contracts;
using Microsoft.AspNetCore.Mvc;
using WebShop.Api.Services;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly PspClient _psp;

    public PaymentsController(PspClient psp)
    {
        _psp = psp;
    }

    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] PspInitRequest request, CancellationToken ct)
    {
        var result = await _psp.InitAsync(request, ct);
        return Ok(result);
    }
}
