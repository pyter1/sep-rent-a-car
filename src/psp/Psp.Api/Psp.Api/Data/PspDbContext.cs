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

        tx.Property(x => x.MerchantOrderId).IsRequired().HasMaxLength(100);
        tx.Property(x => x.Currency).IsRequired().HasMaxLength(3);

        tx.Property(x => x.SuccessUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.FailUrl).IsRequired().HasMaxLength(500);
        tx.Property(x => x.ErrorUrl).IsRequired().HasMaxLength(500);

        tx.HasIndex(x => x.MerchantOrderId);
    }
}
