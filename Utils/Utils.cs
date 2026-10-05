namespace KeyRemappingService.Utils;

public abstract class Utils
{
    public static int ParseHex(string text, string path)
    {
        try
        {
            return Convert.ToInt32(text, 16);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidOperationException($"Invalid hex code '{text}' in {path}", ex);
        }
    }
}