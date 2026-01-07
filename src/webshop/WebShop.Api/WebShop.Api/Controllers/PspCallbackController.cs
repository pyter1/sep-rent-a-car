using Microsoft.AspNetCore.Mvc;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("payment")]
public sealed class PspCallbackController : ControllerBase
{
    [HttpPost("/payment/success")]
    public ActionResult Success([FromBody] object payload)
    {
        Console.WriteLine($"Callback SUCCESS: {DateTime.UtcNow:o} - {payload}");
        return Ok(new { received = "success", payload });
    }

    [HttpPost("/payment/fail")]
    public ActionResult Fail([FromBody] object payload)
    {
        Console.WriteLine($"Callback FAIL: {DateTime.UtcNow:o} - {payload}");
        return Ok(new { received = "fail", payload });
    }

    [HttpPost("/payment/error")]
    public ActionResult Error([FromBody] object payload)
    {
        Console.WriteLine($"Callback ERROR: {DateTime.UtcNow:o} - {payload}");
        return Ok(new { received = "error", payload });
    }
}
