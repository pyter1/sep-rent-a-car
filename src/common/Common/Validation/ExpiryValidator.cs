namespace Common.Validation;

public static class ExpiryValidator
{
    public static bool IsValidNotExpired(int month, int year)
    {
        if (month < 1 || month > 12) return false;
        if (year < 2000 || year > 2100) return false;

        // Valid until end of expiry month
        var now = DateTime.UtcNow;
        var expiryEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month), 23, 59, 59, DateTimeKind.Utc);

        return expiryEnd >= now;
    }
}
