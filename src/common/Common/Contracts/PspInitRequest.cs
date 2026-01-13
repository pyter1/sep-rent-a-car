namespace Common.Contracts;

public sealed record PspInitRequest(
    // Table 1 (WebShop -> PSP)
    string MerchantId,
    string MerchantPassword,
    decimal Amount,
    string Currency,
    string MerchantOrderId,
    DateTime MerchantTimestampUtc,
    string SuccessUrl,
    string FailUrl,
    string ErrorUrl
);
