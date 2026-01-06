using Common.Contracts;

namespace Psp.Api.Data.Entities;

public sealed class PspTransaction
{
    public Guid Id { get; set; }
    public string MerchantOrderId { get; set; } = default!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string SuccessUrl { get; set; } = default!;
    public string FailUrl { get; set; } = default!;
    public string ErrorUrl { get; set; } = default!;

    public Guid? BankPaymentId { get; set; }
}
