using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WebShop.Api.Data;
using WebShop.Api.Data.Entities;

namespace WebShop.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly WebShopDbContext _db;
    private readonly PasswordHasher<WebShopUser> _hasher;
    private readonly IConfiguration _cfg;

    public AuthController(WebShopDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
        _hasher = new PasswordHasher<WebShopUser>();
    }

    public sealed record RegisterRequest(string Email, string Password);
    public sealed record LoginRequest(string Email, string Password);
    public sealed record AuthResponse(string Token, Guid UserId, string Role);

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return BadRequest(new { message = "Email is required." });
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var exists = await _db.Users.AnyAsync(x => x.Email == email, ct);
        if (exists) return Conflict(new { message = "Email already registered." });

        var user = new WebShopUser
        {
            Email = email,
            Role = "Customer",
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, req.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var token = CreateJwt(user);
        return Ok(new AuthResponse(token, user.Id, user.Role));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null) return Unauthorized(new { message = "Invalid credentials." });

        // lockout check
        if (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc.Value > DateTime.UtcNow)
            return Unauthorized(new { message = "Account locked. Try later." });

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password ?? "");
        if (verify == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount += 1;

            // lockout policy: 5 fails -> 10 min
            if (user.FailedLoginCount >= 5)
            {
                user.LockoutUntilUtc = DateTime.UtcNow.AddMinutes(10);
                user.FailedLoginCount = 0; // reset counter after lockout
            }

            await _db.SaveChangesAsync(ct);
            return Unauthorized(new { message = "Invalid credentials." });
        }

        // success: reset counters
        user.FailedLoginCount = 0;
        user.LockoutUntilUtc = null;
        await _db.SaveChangesAsync(ct);

        var token = CreateJwt(user);
        return Ok(new AuthResponse(token, user.Id, user.Role));
    }

    private string CreateJwt(WebShopUser user)
    {
        var key = _cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Missing Jwt:Key configuration.");

        var issuer = _cfg["Jwt:Issuer"] ?? "webshop";
        var audience = _cfg["Jwt:Audience"] ?? "webshop-ui";
        var expMinutes = int.TryParse(_cfg["Jwt:ExpMinutes"], out var m) ? m : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
