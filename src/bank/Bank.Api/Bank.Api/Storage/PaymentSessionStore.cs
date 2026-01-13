using System.Collections.Concurrent;
using Common.Contracts;

namespace Bank.Api.Storage;

public sealed class PaymentSessionStore
{
    private readonly ConcurrentDictionary<Guid, PaymentSession> _sessions = new();

    public PaymentSession Create(string pspMerchantId, string stan, DateTime pspTimestampUtc, Guid pspTxId, decimal amount, string currency, TimeSpan ttl)
    {
        var paymentId = Guid.NewGuid();
        var s = new PaymentSession(
            PaymentId: paymentId,
            PspTransactionId: pspTxId,
            PspMerchantId: pspMerchantId,
            Stan: stan,
            PspTimestampUtc: pspTimestampUtc,
            Amount: amount,
            Currency: currency,
            Status: PaymentStatus.Created,
            Attempted: false,
            ExpiresAtUtc: DateTime.UtcNow.Add(ttl)
        );

        _sessions[paymentId] = s;
        return s;
    }

    public bool TryGet(Guid paymentId, out PaymentSession session) => _sessions.TryGetValue(paymentId, out session);

    public PaymentSession Update(Guid paymentId, Func<PaymentSession, PaymentSession> update)
    {
        while (true)
        {
            if (!_sessions.TryGetValue(paymentId, out var current))
                throw new KeyNotFoundException($"Payment {paymentId} not found.");

            var next = update(current);
            if (_sessions.TryUpdate(paymentId, next, current)) return next;
        }
    }
}

public sealed record PaymentSession(
    Guid PaymentId,
    Guid PspTransactionId,
    string PspMerchantId,
    string Stan,
    DateTime PspTimestampUtc,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    bool Attempted,
    DateTime ExpiresAtUtc
);
