using Microsoft.EntityFrameworkCore;
using Psp.Api.Data.Entities;

namespace Psp.Api.Data;

public sealed class PspDbContext : DbContext
{
    public PspDbContext(DbContextOptions<PspDbContext> options) : base(options) { }

    public DbSet<PspTransaction> Transactions => Set<PspTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tx = modelBuilder.Entity<PspTransaction>();

        tx.ToTable("psp_transactions");
        tx.HasKey(x => x.Id);

        // Table 1
        tx.Property(x => x.MerchantId).IsRequired().HasMaxLength(64);
        tx.Property(x => x.MerchantOrderId).IsRequired().HasMaxLength(100);
        tx.Property(x => x.MerchantTimestampUtc).IsRequired();

        // Core payment data
        tx.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        // Table 2 trace (optional until StartCard is called)
        tx.Property(x => x.Stan).HasMaxLength(32);
        tx.Property(x => x.PspTimestampUtc);

        // Callbacks
        tx.Property(x => x.SuccessUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.FailUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.ErrorUrl).IsRequired().HasMaxLength(500);

        // Indexes to support troubleshooting / reconciliation
        tx.HasIndex(x => new { x.MerchantId, x.MerchantOrderId });
        tx.HasIndex(x => x.MerchantOrderId);
        tx.HasIndex(x => x.Stan);
    }
}
