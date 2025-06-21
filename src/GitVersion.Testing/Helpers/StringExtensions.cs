namespace GitVersion.Testing.Helpers;

internal static class StringExtensions
{
    public static bool EndsWithAnIntId(this string value) =>
        value.Length > 0 && char.IsDigit(value[^1]);

    public static bool IsAnIntId(this string value) =>
        int.TryParse(value, out _);

    public static bool IsAnId(this string value) => IsAnIntId(value) || EndsWithAnIntId(value);
}
