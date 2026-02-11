namespace Psp.Api.Services;

public sealed class CurrencyConverter
{
    private readonly IConfiguration _config;

    public CurrencyConverter(IConfiguration config) => _config = config;

    public decimal Convert(decimal amount, string from, string to)
    {
        from = (from ?? "").Trim().ToUpperInvariant();
        to   = (to   ?? "").Trim().ToUpperInvariant();

        if (from == to) return amount;

        // Config key example: ExchangeRates:EUR:RSD = 117.0
        var key = $"ExchangeRates:{from}:{to}";
        var rateStr = _config[key];

        if (string.IsNullOrWhiteSpace(rateStr) || !decimal.TryParse(rateStr, out var rate) || rate <= 0)
            throw new InvalidOperationException($"Missing/invalid FX rate config: {key}");

        // Keep 2 decimals for display; IPS QR string formatting happens in bank (comma).
        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }
}
