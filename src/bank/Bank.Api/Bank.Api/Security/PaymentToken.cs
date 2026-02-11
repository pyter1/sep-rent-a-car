using System.Security.Cryptography;
using System.Text;

namespace Bank.Api.Security;

public static class PaymentToken
{
    public static string Create(Guid paymentId, string secret)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var msg = Encoding.UTF8.GetBytes(paymentId.ToString("N"));

        using var h = new HMACSHA256(key);
        var mac = h.ComputeHash(msg);

        return Base64Url(mac);
    }

    public static bool Validate(Guid paymentId, string secret, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var expected = Create(paymentId, secret);
        return FixedTimeEquals(expected, token);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ba.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}
