using Common.Contracts;

namespace Bank.Api.Data.Entities;

public sealed class BankPayment
{
    public Guid Id { get; set; }                 // PAYMENT_ID
    public Guid PspTransactionId { get; set; }   // internal correlation (coursework)

    // Table 2 fields (PSP -> Bank)
    public string PspMerchantId { get; set; } = default!;
    public string Stan { get; set; } = default!;
    public DateTime PspTimestampUtc { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public PaymentStatus Status { get; set; }
    public bool Attempted { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool NotifiedPsp { get; set; } = false;
    public PaymentStatus? NotifiedPspStatus { get; set; } = null;
}
