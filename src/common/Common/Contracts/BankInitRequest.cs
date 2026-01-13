namespace Common.Contracts;

public sealed record BankInitRequest(
    // Table 2 (PSP -> Acquirer bank)
    string MerchantId,
    decimal Amount,
    string Currency,
    string Stan,
    DateTime PspTimestampUtc,

    // Internal correlation (kept for easier linking in this coursework project)
    Guid PspTransactionId
);
