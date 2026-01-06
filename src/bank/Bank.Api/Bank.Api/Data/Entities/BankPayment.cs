using Common.Contracts;

namespace Bank.Api.Data.Entities;

public sealed class BankPayment
{
    public Guid Id { get; set; }                 // paymentId
    public Guid PspTransactionId { get; set; }   // link back to PSP
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public PaymentStatus Status { get; set; }
    public bool Attempted { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public bool NotifiedPsp { get; set; } = false;


}
