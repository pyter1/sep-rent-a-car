using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WebShop.Api.Data;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly WebShopDbContext _db;
    public OrdersController(WebShopDbContext db) => _db = db;

    [Authorize(Policy = "Customer")]
    [HttpGet("me")]
    public async Task<IActionResult> MyOrders(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var orders = await _db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => new
            {
                o.MerchantOrderId,
                o.Amount,
                o.Currency,
                status = o.Status.ToString(),
                o.CreatedAtUtc,
                o.PaidAtUtc
            })
            .ToListAsync(ct);

        return Ok(orders);
    }
}
