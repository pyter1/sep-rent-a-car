using Bank.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bank.Api.Data;

public sealed class BankDbContext : DbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

    public DbSet<BankPayment> Payments => Set<BankPayment>();
    public DbSet<BankAuditEvent> AuditEvents => Set<BankAuditEvent>(); // NEW

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
        p.Property(x => x.CardBrand).HasMaxLength(16);
        p.Property(x => x.PanLast4).HasMaxLength(4);
        p.HasIndex(x => x.PanLast4);

        p.HasIndex(x => new { x.PspMerchantId, x.Stan, x.PspTimestampUtc }).IsUnique(false);

        // NEW: audit table
        var a = modelBuilder.Entity<BankAuditEvent>();
        a.ToTable("bank_audit_events");
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
        a.HasIndex(x => x.BankPaymentId);
        a.HasIndex(x => x.PspTransactionId);
        a.HasIndex(x => x.Stan);
    }
}
