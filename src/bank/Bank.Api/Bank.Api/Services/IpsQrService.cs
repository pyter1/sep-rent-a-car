using System.Globalization;
using System.Text;
using Bank.Api.Data.Entities;

namespace Bank.Api.Services;

/// <summary>
/// Generator + validator for NBS IPS QR (text payload), implemented "po uzoru" na NBS IPS.
/// Payload is a string with TAG:VALUE segments separated by '|'.
/// </summary>
public sealed class IpsQrService
{
    private readonly IpsQrOptions _opt;

    public IpsQrService(IConfiguration config)
    {
        _opt = config.GetSection("IpsQr").Get<IpsQrOptions>() ?? new IpsQrOptions();
    }

    public sealed record IpsQrOptions(
        string ReceiverAccount,
        string ReceiverName,
        string PaymentCode,
        string PurposePrefix)
    {
        public IpsQrOptions() : this(
            ReceiverAccount: "845000000040484987",
            ReceiverName: "SEP Rent-a-Car",
            PaymentCode: "289",
            PurposePrefix: "SEP")
        {
        }
    }

    public sealed record IpsQrFields(
        string K,
        string V,
        string C,
        string R,
        string N,
        string I,
        string SF,
        string? P,
        string? S,
        string? RO,
        Guid? EmbeddedPaymentId);

    public sealed record IpsQrResult(bool Ok, IpsQrFields? Fields, IReadOnlyList<string> Errors);

    /// <summary>
    /// Builds IPS QR payload for a specific bank payment session.
    /// Enforces RSD to match NBS IPS QR validator expectations.
    /// Embeds BankPaymentId token inside tag S so your scanner can resolve the payment.
    /// </summary>
    public (bool Ok, string? Payload, IReadOnlyList<string> Errors) BuildForPayment(BankPayment p)
    {
        var errs = new List<string>();

        if (!string.Equals(p.Currency, "RSD", StringComparison.OrdinalIgnoreCase))
            errs.Add("IPS QR supports RSD only. Set transaction currency to RSD for QR payments.");

        var receiverAccount = DigitsOnly(_opt.ReceiverAccount);
        if (receiverAccount.Length < 10)
            errs.Add("IpsQr:ReceiverAccount must contain an account number (digits only).");

        var receiverName = (_opt.ReceiverName ?? string.Empty).Trim();
        if (receiverName.Length == 0)
            errs.Add("IpsQr:ReceiverName is required.");
        if (receiverName.Length > 70)
            errs.Add("IpsQr:ReceiverName must be <= 70 characters.");

        var sf = (_opt.PaymentCode ?? string.Empty).Trim();
        if (sf.Length != 3 || !sf.All(char.IsDigit))
            errs.Add("IpsQr:PaymentCode must be a 3-digit payment code (e.g., 289).");

        if (errs.Count > 0)
            return (false, null, errs);

        // Token in tag S (svrha) used to resolve BankPaymentId during scan.
        // S is optional, single-line, max 35 chars.
        var token = IpsQrToken.EncodePaymentId(p.Id);
        var purpose = ($"{_opt.PurposePrefix} {token}").Trim();
        if (purpose.Length > 35) purpose = purpose[..35];

        var sb = new StringBuilder();
        sb.Append("K:PR");
        sb.Append("|V:01");
        sb.Append("|C:1");
        sb.Append("|R:").Append(receiverAccount);
        sb.Append("|N:").Append(receiverName);
        sb.Append("|I:").Append(FormatIrsd(p.Amount));
        sb.Append("|SF:").Append(sf);
        sb.Append("|S:").Append(purpose);

        // Must NOT end with '|'
        return (true, sb.ToString(), Array.Empty<string>());
    }

    /// <summary>
    /// Parses + validates IPS QR payload and returns normalized fields + errors.
    /// </summary>
    public IpsQrResult ParseAndValidate(string? payload)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(payload))
            return new IpsQrResult(false, null, new[] { "Empty QR payload." });

        payload = payload.Trim();

        if (payload.EndsWith('|'))
            errors.Add("Payload must not end with '|'.");

        var parts = payload.Split('|', StringSplitOptions.RemoveEmptyEntries);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            var idx = part.IndexOf(':');
            if (idx <= 0)
            {
                errors.Add($"Invalid segment '{part}'. Expected TAG:VALUE.");
                continue;
            }

            var tag = part[..idx];
            var value = part[(idx + 1)..];

            if (tag.Length is < 1 or > 3)
            {
                errors.Add($"Invalid tag '{tag}'.");
                continue;
            }

            if (map.ContainsKey(tag))
            {
                errors.Add($"Duplicate tag '{tag}'.");
                continue;
            }

            map[tag] = value;
        }

        // Mandatory tags: K, V, C, R, N, I, SF
        string req(string t)
        {
            if (!map.TryGetValue(t, out var v) || string.IsNullOrWhiteSpace(v))
                errors.Add($"Missing tag '{t}'.");
            return v ?? string.Empty;
        }

        var k = req("K");
        var vv = req("V");
        var c = req("C");
        var r = req("R");
        var n = req("N");
        var i = req("I");
        var sf = req("SF");

        map.TryGetValue("P", out var p);
        map.TryGetValue("S", out var s);
        map.TryGetValue("RO", out var ro);

        if (!string.Equals(k, "PR", StringComparison.Ordinal))
            errors.Add("Tag K must be 'PR'.");

        if (!string.Equals(vv, "01", StringComparison.Ordinal))
            errors.Add("Tag V must be '01'.");

        if (!string.Equals(c, "1", StringComparison.Ordinal))
            errors.Add("Tag C must be '1'.");

        // R: digits only
        if (!string.IsNullOrWhiteSpace(r))
        {
            if (r.Any(ch => !char.IsDigit(ch))) errors.Add("Tag R must contain digits only.");
            if (r.Contains('\n') || r.Contains('\r')) errors.Add("Tag R must be single-line.");
        }

        // N: max 70, up to 3 lines
        if (!string.IsNullOrWhiteSpace(n))
        {
            if (n.Length > 70) errors.Add("Tag N must be <= 70 characters.");
            var lines = n.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            if (lines.Length > 3) errors.Add("Tag N must have at most 3 lines.");
        }

        // I: must start with RSD, must contain decimal comma
        if (!string.IsNullOrWhiteSpace(i))
        {
            if (!i.StartsWith("RSD", StringComparison.Ordinal))
                errors.Add("Tag I must start with 'RSD'.");
            if (!i.Contains(','))
                errors.Add("Tag I must contain a decimal comma (',').");
            if (i.Contains(' '))
                errors.Add("Tag I must not contain spaces.");
            if (i.Length is < 5 or > 18)
                errors.Add("Tag I length must be between 5 and 18 characters.");
        }

        // SF: 3 digits
        if (!string.IsNullOrWhiteSpace(sf))
        {
            if (sf.Length != 3 || !sf.All(char.IsDigit))
                errors.Add("Tag SF must be a 3-digit payment code.");
        }

        // S: optional, <= 35, single-line
        if (!string.IsNullOrEmpty(s))
        {
            if (s.Length > 35) errors.Add("Tag S must be <= 35 characters.");
            if (s.Contains('\n') || s.Contains('\r')) errors.Add("Tag S must be single-line.");
        }

        // RO: optional, <= 25, single-line
        if (!string.IsNullOrEmpty(ro))
        {
            if (ro.Length > 25) errors.Add("Tag RO must be <= 25 characters.");
            if (ro.Contains('\n') || ro.Contains('\r')) errors.Add("Tag RO must be single-line.");
            if (ro.EndsWith(' ')) errors.Add("Tag RO must not end with spaces.");
        }

        Guid? embeddedPaymentId = null;
        if (!string.IsNullOrWhiteSpace(s))
        {
            embeddedPaymentId = IpsQrToken.TryDecodePaymentIdFromPurpose(s, _opt.PurposePrefix);
        }

        var ok = errors.Count == 0;
        var fields = new IpsQrFields(k, vv, c, r, n, i, sf, p, s, ro, embeddedPaymentId);
        return new IpsQrResult(ok, fields, errors);
    }

    private static string DigitsOnly(string input)
        => new string((input ?? string.Empty).Where(char.IsDigit).ToArray());

    // Example: RSD1025,  or RSD1025,1 or RSD1025,12
    private static string FormatIrsd(decimal amount)
    {
        var rounded = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var inv = rounded.ToString("0.##", CultureInfo.InvariantCulture); // 1025 or 1025.1 or 1025.12
        if (!inv.Contains('.')) inv += ".";
        return "RSD" + inv.Replace('.', ',');
    }
}

internal static class IpsQrToken
{
    // 16 bytes GUID -> Base64URL without padding => 22 chars
    public static string EncodePaymentId(Guid id)
    {
        var b64 = Convert.ToBase64String(id.ToByteArray());
        return b64.TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static Guid? TryDecodePaymentIdFromPurpose(string purpose, string prefix)
    {
        var p = (purpose ?? string.Empty).Trim();
        if (p.Length == 0) return null;

        var expectedPrefix = (prefix ?? string.Empty).Trim();
        if (expectedPrefix.Length == 0) return null;

        if (!p.StartsWith(expectedPrefix + " ", StringComparison.Ordinal)) return null;

        var token = p[(expectedPrefix.Length + 1)..].Trim();
        if (token.Length == 0) return null;

        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
                case 0: break;
                default: return null;
            }

            var bytes = Convert.FromBase64String(padded);
            if (bytes.Length != 16) return null;
            return new Guid(bytes);
        }
        catch
        {
            return null;
        }
    }
}
