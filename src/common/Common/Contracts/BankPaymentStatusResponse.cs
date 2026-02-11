namespace Common.Contracts;

public sealed record BankPaymentStatusResponse(
    Guid PaymentId,
    Guid PspTransactionId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    PaymentMethodType PaymentMethod,
    bool Attempted,
    DateTime ExpiresAtUtc,
    PaymentStatus? NotifiedPspStatus,
    string? CardBrand,
    string? PanFirst6,
    string? PanLast4,
    string PspMerchantId,
    string Stan,
    DateTime PspTimestampUtc
);
