namespace WebShop.Api.Contracts;

public sealed record PspCallbackPayload(
    Guid pspTransactionId,
    string merchantId,
    string merchantOrderId,
    Guid? bankPaymentId,
    string? stan,
    string status,

    // PCI-safe metadata
    string? cardBrand = null,
    string? panLast4 = null
);
