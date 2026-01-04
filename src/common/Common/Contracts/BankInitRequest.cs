namespace Common.Contracts;

public sealed record BankInitRequest(
    Guid PspTransactionId,
    decimal Amount,
    string Currency
);
