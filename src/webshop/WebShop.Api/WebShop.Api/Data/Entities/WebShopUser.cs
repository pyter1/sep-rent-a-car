namespace WebShop.Api.Data.Entities;

public sealed class WebShopUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;

    // "Customer" | "Admin"
    public string Role { get; set; } = "Customer";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public int FailedLoginCount { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }
}
