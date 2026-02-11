using Microsoft.EntityFrameworkCore;
using WebShop.Api.Data.Entities;

namespace WebShop.Api.Data;

public sealed class WebShopDbContext : DbContext
{
    public WebShopDbContext(DbContextOptions<WebShopDbContext> options) : base(options) { }

    public DbSet<WebShopUser> Users => Set<WebShopUser>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<WebShopAuditEvent> AuditEvents => Set<WebShopAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var u = modelBuilder.Entity<WebShopUser>();
        u.ToTable("webshop_users");
        u.HasKey(x => x.Id);
        u.Property(x => x.Email).IsRequired().HasMaxLength(256);
        u.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
        u.Property(x => x.Role).IsRequired().HasMaxLength(32);
        u.HasIndex(x => x.Email).IsUnique();

        var o = modelBuilder.Entity<Order>();
        o.ToTable("webshop_orders");
        o.HasKey(x => x.Id);
        o.Property(x => x.MerchantOrderId).IsRequired().HasMaxLength(100);
        o.Property(x => x.Currency).IsRequired().HasMaxLength(3);
        o.HasIndex(x => x.MerchantOrderId).IsUnique();
        o.HasIndex(x => x.UserId);
        o.HasIndex(x => x.Status);
        o.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        o.Property(x => x.Stan).HasMaxLength(32);
        o.Property(x => x.CardBrand).HasMaxLength(32);
        o.Property(x => x.PanLast4).HasMaxLength(4);
        o.HasIndex(x => x.BankPaymentId);


        var a = modelBuilder.Entity<WebShopAuditEvent>();
        a.ToTable("webshop_audit_events");
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
        a.HasIndex(x => x.MerchantOrderId);
        a.HasIndex(x => x.PspTransactionId);
    }
}
