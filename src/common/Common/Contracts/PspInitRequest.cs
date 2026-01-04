namespace Common.Contracts;

public sealed record PspInitRequest(
    string MerchantOrderId,
    decimal Amount,
    string Currency,
    string SuccessUrl,
    string FailUrl,
    string ErrorUrl
);
