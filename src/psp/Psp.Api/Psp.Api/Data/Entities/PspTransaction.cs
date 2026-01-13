using Common.Contracts;

namespace Psp.Api.Data.Entities;

public sealed class PspTransaction
{
    public Guid Id { get; set; }

    // Table 1 fields (WebShop -> PSP)
    public string MerchantId { get; set; } = default!;
    public string MerchantOrderId { get; set; } = default!;
    public DateTime MerchantTimestampUtc { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;

    // Table 2 fields (PSP -> Bank)
    public string? Stan { get; set; }
    public DateTime? PspTimestampUtc { get; set; }

    public TransactionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string SuccessUrl { get; set; } = default!;
    public string FailUrl { get; set; } = default!;
    public string ErrorUrl { get; set; } = default!;

    public Guid? BankPaymentId { get; set; }

    public bool MerchantNotified { get; set; } = false;
    public DateTime? MerchantNotifiedAtUtc { get; set; }
    public int MerchantNotifyAttempts { get; set; } = 0;
    public string? MerchantNotifyLastError { get; set; }
}
