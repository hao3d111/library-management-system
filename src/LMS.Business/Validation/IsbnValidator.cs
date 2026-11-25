namespace LMS.Business.Validation;

public static class IsbnValidator
{
    public static string Normalize(string isbn)
    {
        return isbn.Trim().Replace(" ", string.Empty).ToUpperInvariant();
    }

    public static bool IsValid(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            return false;
        }

        var cleaned = isbn.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();

        if (cleaned.Length == 10)
        {
            return IsValidIsbn10(cleaned);
        }

        if (cleaned.Length == 13)
        {
            return IsValidIsbn13(cleaned);
        }

        return false;
    }

    private static bool IsValidIsbn10(string isbn10)
    {
        int sum = 0;
        for (int i = 0; i < 9; i++)
        {
            if (!char.IsDigit(isbn10[i]))
            {
                return false;
            }
            sum += (isbn10[i] - '0') * (10 - i);
        }

        char last = isbn10[9];
        int lastValue;
        if (last == 'X')
        {
            lastValue = 10;
        }
        else if (char.IsDigit(last))
        {
            lastValue = last - '0';
        }
        else
        {
            return false;
        }

        sum += lastValue;
        return sum % 11 == 0;
    }

    private static bool IsValidIsbn13(string isbn13)
    {
        if (!isbn13.StartsWith("978") && !isbn13.StartsWith("979"))
        {
            return false;
        }

        int sum = 0;
        for (int i = 0; i < 13; i++)
        {
            if (!char.IsDigit(isbn13[i]))
            {
                return false;
            }
            int digit = isbn13[i] - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        return sum % 10 == 0;
    }
}
