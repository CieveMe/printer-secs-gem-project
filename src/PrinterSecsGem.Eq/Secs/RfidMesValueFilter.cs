namespace PrinterSecsGem.Eq.Secs;

public static class RfidMesValueFilter
{
    public static string Filter(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var filtered = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if ((character is >= 'A' and <= 'Z') ||
                (character is >= 'a' and <= 'z') ||
                (character is >= '0' and <= '9'))
            {
                filtered.Append(character);
            }
        }

        return filtered.ToString();
    }
}
