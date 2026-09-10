namespace LuoTianyiPet.Core;

public static class Guard
{
    public static void NotNull(object? value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }
    }

    public static void NotNullOrWhiteSpace(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }
}

public static class Numeric
{
    public static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static float Clamp(float value, float minimum, float maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static double Clamp(double value, double minimum, double maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

public static class SharedRandom
{
    private static readonly object Sync = new();
    private static readonly Random Instance = new();

    public static int Next(int maximum)
    {
        lock (Sync)
        {
            return Instance.Next(maximum);
        }
    }

    public static int Next(int minimum, int maximum)
    {
        lock (Sync)
        {
            return Instance.Next(minimum, maximum);
        }
    }

    public static double NextDouble()
    {
        lock (Sync)
        {
            return Instance.NextDouble();
        }
    }
}

public static class TextParsing
{
    public static string[] SplitAndTrim(string value, char separator) =>
        value.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();

    public static string[] SplitAndTrim(string value, char[] separators) =>
        value.Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToArray();
}
