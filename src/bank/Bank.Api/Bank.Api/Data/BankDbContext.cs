using Bank.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bank.Api.Data;

public sealed class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<BankPayment> Payments => Set<BankPayment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var p = modelBuilder.Entity<BankPayment>();

        p.ToTable("bank_payments");
        p.HasKey(x => x.Id);

        p.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        p.Property(x => x.PspMerchantId).IsRequired().HasMaxLength(64);
        p.Property(x => x.Stan).IsRequired().HasMaxLength(32);

        p.HasIndex(x => x.PspTransactionId);
        p.HasIndex(x => x.Status);
        p.HasIndex(x => x.ExpiresAtUtc);

        // Table 2 trace lookup (used for idempotency / reconciliation if init response is lost)
        p.HasIndex(x => new { x.PspMerchantId, x.Stan, x.PspTimestampUtc }).IsUnique(false);
    }
}
