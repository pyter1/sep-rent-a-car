using System.Security.Cryptography;
using System.Text;

namespace Common.Security;

public static class HmacSigner
{
    public const string TimestampHeader = "X-TimestampUtc";
    public const string SignatureHeader = "X-Signature";

    public static string CreateTimestampUtc(DateTime? nowUtc = null)
        => (nowUtc ?? DateTime.UtcNow).ToString("O"); // ISO 8601

    public static string ComputeSignatureBase64(string secret, string canonical)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(canonical);

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    public static string BuildCanonical(string timestampUtc, string method, string pathAndQuery, string body)
        => $"{timestampUtc}\n{method.ToUpperInvariant()}\n{pathAndQuery}\n{body}";
}
