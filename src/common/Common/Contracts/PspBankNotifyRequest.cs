namespace Common.Contracts;

public sealed record PspBankNotifyRequest(
    Guid PspTransactionId,
    Guid BankPaymentId,
    PaymentStatus Status,
    string? Stan = null,
    DateTime? AcquirerTimestampUtc = null
);
