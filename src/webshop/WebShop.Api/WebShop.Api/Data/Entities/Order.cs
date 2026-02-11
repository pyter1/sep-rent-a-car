namespace WebShop.Api.Data.Entities;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public WebShopUser? User { get; set; }

    public string MerchantOrderId { get; set; } = default!; // unique

    public Guid? PspTransactionId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;

    public OrderStatus Status { get; set; } = OrderStatus.Initiated;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public Guid? BankPaymentId { get; set; }
    public string? Stan { get; set; }

    public string? CardBrand { get; set; }
    public string? PanLast4 { get; set; }

}
