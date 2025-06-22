using System.Text.RegularExpressions;

namespace GitVersion.Testing.Helpers;

public static partial class RegexReplacer
{
    [GeneratedRegex("[^a-zA-Z0-9]")]
    public static partial Regex NonAlphanumericRegex();
}
