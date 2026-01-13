namespace WebShop.Api.Contracts;

public sealed record WebShopInitRequest(
    decimal Amount,
    string Currency,
    string MerchantOrderId
);
