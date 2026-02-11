using System.Security.Cryptography;
using System.Text;

namespace Common.Security;

public static class HmacVerifier
{
    public static bool ConstantTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(ba, bb);
    }

    public static bool TryParseTimestampUtc(string? timestampUtc, out DateTime ts)
        => DateTime.TryParse(timestampUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out ts);

    public static bool IsFresh(DateTime timestampUtc, TimeSpan maxSkew)
    {
        var now = DateTime.UtcNow;
        var diff = (now - timestampUtc).Duration();
        return diff <= maxSkew;
    }
}
