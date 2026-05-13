namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Single source of truth for deriving the YAML emission key — the GitVersion
/// branch-family identity — from a user-supplied branch pattern.
/// </summary>
/// <remarks>
/// Both <see cref="AmbiguityDetector"/> and <see cref="YamlEmitter"/> consult
/// this helper. Divergence between the two would break the emission key-space
/// injectivity invariant (DEC-018): the detector would let two patterns through
/// that the emitter then collides on, or reject patterns that would in fact
/// emit cleanly. Locating the derivation rule here makes the coupling explicit
/// and prevents drift as the rule evolves.
/// </remarks>
public static class BranchFamilyKey
{
    /// <summary>
    /// Derives the family key for a branch pattern: the prefix before the first
    /// '/' (e.g. "feature/Login" → "feature"), or the whole pattern when no
    /// '/' is present (e.g. "master" → "master").
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="branchPattern"/> is null.</exception>
    public static string Derive(string branchPattern)
    {
        ArgumentNullException.ThrowIfNull(branchPattern);
        var slash = branchPattern.IndexOf('/');
        return slash >= 0 ? branchPattern[..slash] : branchPattern;
    }
}
