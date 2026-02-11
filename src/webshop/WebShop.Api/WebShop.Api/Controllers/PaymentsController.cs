using Common.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebShop.Api.Contracts;
using WebShop.Api.Data;
using WebShop.Api.Data.Entities;
using WebShop.Api.Services;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly PspClient _psp;
    private readonly IConfiguration _config;
    private readonly WebShopDbContext _db;

    public PaymentsController(PspClient psp, IConfiguration config, WebShopDbContext db)
    {
        _psp = psp;
        _config = config;
        _db = db;
    }

    [Authorize(Policy = "Customer")]
    [HttpPost("init")]
    public async Task<ActionResult<PspInitResponse>> Init([FromBody] WebShopInitRequest request, CancellationToken ct)
    {
        if (request.Amount <= 0) return BadRequest(new { message = "Amount must be > 0." });
        if (string.IsNullOrWhiteSpace(request.Currency)) return BadRequest(new { message = "Currency is required." });
        if (string.IsNullOrWhiteSpace(request.MerchantOrderId)) return BadRequest(new { message = "MerchantOrderId is required." });

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token (missing user id)." });

        var merchantOrderId = request.MerchantOrderId.Trim();

        // Enforce uniqueness (PCI-ish: prevents replay/weird duplicates)
        var exists = await _db.Orders.AnyAsync(o => o.MerchantOrderId == merchantOrderId, ct);
        if (exists) return Conflict(new { message = "MerchantOrderId already exists." });

        // Create order record (Initiated)
        var order = new Order
        {
            UserId = userId,
            MerchantOrderId = merchantOrderId,
            Amount = request.Amount,
            Currency = request.Currency.Trim().ToUpperInvariant(),
            Status = OrderStatus.Initiated,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        var merchantId = _config["Psp:MerchantId"];
        var merchantPassword = _config["Psp:MerchantPassword"];

        if (string.IsNullOrWhiteSpace(merchantId) || string.IsNullOrWhiteSpace(merchantPassword))
            return StatusCode(500, new { message = "Missing Psp:MerchantId / Psp:MerchantPassword in WebShop.Api config." });

        var publicBaseUrl = (_config["PublicBaseUrl"] ?? "http://localhost:7003").TrimEnd('/');

        var reqForPsp = new PspInitRequest(
            MerchantId: merchantId,
            MerchantPassword: merchantPassword,
            Amount: order.Amount,
            Currency: order.Currency,
            MerchantOrderId: order.MerchantOrderId,
            MerchantTimestampUtc: DateTime.UtcNow,
            SuccessUrl: $"{publicBaseUrl}/payment/success",
            FailUrl: $"{publicBaseUrl}/payment/fail",
            ErrorUrl: $"{publicBaseUrl}/payment/error"
        );

        var result = await _psp.InitAsync(reqForPsp, ct);

        // Persist PSP transaction id + Redirected state
        order.PspTransactionId = result.TransactionId;
        order.Status = OrderStatus.Redirected;
        order.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(result);
    }
}
