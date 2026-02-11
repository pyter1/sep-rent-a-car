namespace Bank.Api.Data.Entities;

public sealed class BankAuditEvent
{
    public long Id { get; set; } // bigserial identity (migration will set this)

    public DateTime TimestampUtc { get; set; }
    public string Service { get; set; } = "bank";

    public int EventType { get; set; } // store as int (maps to AuditEventType)
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

    public string Result { get; set; } = default!;   // "OK"/"FAIL"
    public string? DetailsJson { get; set; }         // text/jsonb, but TEXT is simplest
}
