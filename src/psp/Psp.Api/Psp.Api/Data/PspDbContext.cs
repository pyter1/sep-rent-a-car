using Microsoft.EntityFrameworkCore;
using Psp.Api.Data.Entities;

namespace Psp.Api.Data;

public sealed class PspDbContext : DbContext
{
    public PspDbContext(DbContextOptions<PspDbContext> options) : base(options) { }

    public DbSet<PspTransaction> Transactions => Set<PspTransaction>();
    public DbSet<PspAuditEvent> AuditEvents => Set<PspAuditEvent>(); // NEW

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tx = modelBuilder.Entity<PspTransaction>();

        tx.ToTable("psp_transactions");
        tx.HasKey(x => x.Id);

        tx.Property(x => x.MerchantId).IsRequired().HasMaxLength(64);
        tx.Property(x => x.MerchantOrderId).IsRequired().HasMaxLength(100);
        tx.Property(x => x.MerchantTimestampUtc).IsRequired();
        tx.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        tx.Property(x => x.Stan).HasMaxLength(32);
        tx.Property(x => x.PspTimestampUtc);
        // NEW: PCI-safe card fragments + lifecycle timestamp
        tx.Property(x => x.CardBrand).HasMaxLength(32);
        tx.Property(x => x.PanFirst6).HasMaxLength(6);
        tx.Property(x => x.PanLast4).HasMaxLength(4);

        tx.Property(x => x.UpdatedAtUtc).IsRequired();

        // Useful for reconciliation queries
        tx.HasIndex(x => x.BankPaymentId);

        tx.Property(x => x.SuccessUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.FailUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.ErrorUrl).IsRequired().HasMaxLength(500);

        tx.HasIndex(x => new { x.MerchantId, x.MerchantOrderId });
        tx.HasIndex(x => x.MerchantOrderId);
        tx.HasIndex(x => x.Stan);

        // NEW: audit table
        var a = modelBuilder.Entity<PspAuditEvent>();
        a.ToTable("psp_audit_events");
        a.HasKey(x => x.Id);

        a.Property(x => x.Service).IsRequired().HasMaxLength(32);
        a.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);
        a.Property(x => x.Result).IsRequired().HasMaxLength(8);

        a.Property(x => x.ActorType).HasMaxLength(32);
        a.Property(x => x.ActorId).HasMaxLength(128);
        a.Property(x => x.MerchantId).HasMaxLength(64);
        a.Property(x => x.MerchantOrderId).HasMaxLength(100);
        a.Property(x => x.Stan).HasMaxLength(32);
        a.Property(x => x.Ip).HasMaxLength(64);
        a.Property(x => x.UserAgent).HasMaxLength(300);

        a.HasIndex(x => x.TimestampUtc);
        a.HasIndex(x => x.CorrelationId);
        a.HasIndex(x => x.PspTransactionId);
        a.HasIndex(x => x.BankPaymentId);
        a.HasIndex(x => x.MerchantOrderId);
        a.HasIndex(x => x.Stan);
    }
}
