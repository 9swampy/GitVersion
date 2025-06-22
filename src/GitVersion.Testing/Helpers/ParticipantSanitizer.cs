namespace GitVersion.Testing.Helpers;

public static class ParticipantSanitizer
{
    /// <summary>
    /// Converts a participant identifier to a standardized format.
    /// </summary>
    /// <param name="participant">The participant identifier to convert. This value cannot be null, empty, or consist only of whitespace.</param>
    /// <returns>A string representing the converted participant identifier. If the input contains a folder separator ('/'), the
    /// portion after the separator is processed recursively. If the input is in kebab-case, it is converted to
    /// PascalCase. Otherwise, the input is returned unchanged.</returns>
    public static string SanitizeParticipant(string participant)
    {
        GuardAgainstInvalidParticipants(participant);

        var folderIndex = participant.IndexOf('/');
        if (folderIndex > -1)
        {
            return SplitOnFolderAndRecurseSuffix(folderIndex, participant);
        }

        if (IsKebabCase(participant))
        {
            return EnsurePascalCase(participant);
        }

        return participant;
    }

    public static string RegexSanitizeParticipant(string participant)
    {
        GuardAgainstInvalidParticipants(participant);

        return RegexReplacer.NonAlphanumericRegex().Replace(participant, "_");
    }

    private static string SplitOnFolderAndRecurseSuffix(int folderIndex, string input)
    {
        var folder = input[..folderIndex];
        if (IsKebabCase(folder))
        {
            var parts = folder.Split('-');
            if (parts[0].IsAnId())
            {
                folder = parts[0] + EnsurePascalCase(string.Join("-", parts.Skip(1)));
            }
            else
            {
                folder = EnsurePascalCase(folder);
            }
        }

        var suffix = input[(folderIndex + 1)..];
        var nextFolderIndex = suffix.IndexOf('/');
        if (ThereIsAnotherFolder(nextFolderIndex))
        {
            return $"{folder}_{SplitOnFolderAndRecurseSuffix(nextFolderIndex, suffix)}";
        }

        return $"{folder}_{EnsurePascalCase(suffix)}";
    }

    private static string EnsureFirstCharUpper(string input) => char.ToUpperInvariant(input[0]) + input[1..];

    private static bool IsKebabCase(string value) => value.Contains('-');

    private static bool ThereIsAnotherFolder(int folderIndex) => folderIndex > -1;

    private static string EnsurePascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        if (input.Length == 1) return input.ToUpperInvariant();
        if (IsKebabCase(input))
        {
            var parts = input.Split('-');
            if (parts[0].IsAnId())
            {
                return parts.Length == 1
                    ? parts[0]
                    : parts[0] + "_" + EnsurePascalCase(string.Join("-", parts.Skip(1)));
            }

            return ToPascalCase(parts);
        }

        return input.IsAnId()
            ? input
            : EnsureFirstCharUpper(input);
    }

    private static string ToPascalCase(string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(EnsurePascalCase(part));
        }

        return sb.ToString();
    }

    private static void GuardAgainstInvalidParticipants(string participant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participant);
        if (participant.EndsWith('/'))
        {
            throw new ArgumentException("The value cannot end with a folder separator ('/').", nameof(participant));
        }
    }
}
