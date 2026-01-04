namespace Common.Contracts;

public sealed record BankInitResponse(
    Guid PaymentId,
    string PaymentUrl
);
