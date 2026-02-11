namespace Psp.Api.Data.Entities;

public sealed class PspAuditEvent
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }
    public string Service { get; set; } = "psp";

    public int EventType { get; set; }
    public string CorrelationId { get; set; } = default!;

    public string? ActorType { get; set; }
    public string? ActorId { get; set; }

    public string? MerchantId { get; set; }
    public string? MerchantOrderId { get; set; }

    public Guid? PspTransactionId { get; set; }
    public Guid? BankPaymentId { get; set; }

    public string? Stan { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }

    public string Result { get; set; } = default!;
    public string? DetailsJson { get; set; }
}
