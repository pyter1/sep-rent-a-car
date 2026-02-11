namespace Common.Security;

public static class PanMasking
{
    public static string MaskLast4(string? last4)
        => string.IsNullOrWhiteSpace(last4) ? "****" : $"**** **** **** {last4}";
}
