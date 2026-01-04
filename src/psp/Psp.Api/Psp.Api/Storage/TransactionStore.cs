using System.Collections.Concurrent;
using Common.Contracts;

namespace Psp.Api.Storage;

public sealed class TransactionStore
{
    private readonly ConcurrentDictionary<Guid, TransactionRecord> _tx = new();

    public TransactionRecord Create(PspInitRequest req)
    {
        var id = Guid.NewGuid();
        var record = new TransactionRecord(
            TransactionId: id,
            MerchantOrderId: req.MerchantOrderId,
            Amount: req.Amount,
            Currency: req.Currency,
            Status: TransactionStatus.Created,
            CreatedAtUtc: DateTime.UtcNow,
            SuccessUrl: req.SuccessUrl,
            FailUrl: req.FailUrl,
            ErrorUrl: req.ErrorUrl,
            BankPaymentId: null
        );

        _tx[id] = record;
        return record;
    }

    public bool TryGet(Guid id, out TransactionRecord? record) => _tx.TryGetValue(id, out record);

    public TransactionRecord SetBankPayment(Guid txId, Guid bankPaymentId)
        => Update(txId, cur => cur with { BankPaymentId = bankPaymentId });

    public TransactionRecord SetStatus(Guid txId, TransactionStatus status)
        => Update(txId, cur => cur with { Status = status });

    private TransactionRecord Update(Guid txId, Func<TransactionRecord, TransactionRecord> update)
    {
        while (true)
        {
            if (!_tx.TryGetValue(txId, out var current) || current is null)
                throw new KeyNotFoundException("Transaction not found.");

            var next = update(current);
            if (_tx.TryUpdate(txId, next, current)) return next;
        }
    }
}

public sealed record TransactionRecord(
    Guid TransactionId,
    string MerchantOrderId,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    DateTime CreatedAtUtc,
    string SuccessUrl,
    string FailUrl,
    string ErrorUrl,
    Guid? BankPaymentId
);
