namespace Common.Contracts;

public sealed record PspBankNotifyRequest(
    Guid PspTransactionId,
    Guid BankPaymentId,
    PaymentStatus Status,
    string? Stan = null,
    DateTime? AcquirerTimestampUtc = null,

    // PCI-safe card metadata (never full PAN, never CVV)
    string? CardBrand = null,
    string? PanFirst6 = null,
    string? PanLast4 = null
);
