using GitVersion.Core;

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

        return RegexPatterns.Output.SanitizeParticipantRegex.Replace(participant, "_");
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
