namespace Common.Contracts;

public sealed record PspInitResponse(
    Guid TransactionId,
    string RedirectUrl
);
