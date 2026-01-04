namespace Common.Contracts;

public sealed record PspBankNotifyRequest(
    Guid PspTransactionId,
    Guid BankPaymentId,
    PaymentStatus Status
);
