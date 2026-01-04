namespace Common.Validation;

public static class Luhn
{
    public static bool IsValid(string pan)
    {
        if (string.IsNullOrWhiteSpace(pan)) return false;
        pan = new string(pan.Where(char.IsDigit).ToArray());
        if (pan.Length < 12 || pan.Length > 19) return false;

        int sum = 0;
        bool alternate = false;

        for (int i = pan.Length - 1; i >= 0; i--)
        {
            int n = pan[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }

        return (sum % 10) == 0;
    }
}
