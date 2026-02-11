namespace Common.Observability;

public sealed record AuditEventDto(
    DateTime TimestampUtc,
    string Service,
    AuditEventType EventType,

    string? ActorType,
    string? ActorId,

    string CorrelationId,

    string? MerchantId,
    string? MerchantOrderId,

    Guid? PspTransactionId,
    Guid? BankPaymentId,

    string? Stan,

    string? Ip,
    string? UserAgent,

    string Result,          // "OK" | "FAIL"
    string? DetailsJson     // never PAN/CVV
);
