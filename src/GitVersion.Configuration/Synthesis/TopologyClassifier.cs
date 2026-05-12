using System.Text.RegularExpressions;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Classifies a set of branch name patterns (Layer 1 input) into a known topology kind
/// and selects the closest matching exemplar dictionary.
/// </summary>
/// <remarks>
/// Pure function — no YAML emission, no Git access, no configuration loading.
/// Topology is inferred from structural signals in the branch patterns:
///   develop present + release pattern present → GitFlow
///   primary present + feature present + no develop → TrunkBased
///   only one signal → Hybrid
///   insufficient signals → Unknown
/// </remarks>
public sealed class TopologyClassifier
{
    private static readonly Regex PrimaryBranch = new(@"^(main|master)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DevelopBranch = new(@"^dev(elop)?(ment)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReleaseBranch = new(@"^releases?([/\-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FeatureBranch = new(@"^features?([/\-]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly TopologyClassification Hybrid = new(TopologyKind.Hybrid, null);
    private static readonly TopologyClassification Unknown = new(TopologyKind.Unknown, null);

    /// <summary>
    /// Classifies branch name patterns into a topology kind and exemplar.
    /// </summary>
    /// <param name="branchPatterns">
    /// User-supplied branch name patterns, e.g. "master", "develop", "feature/Branch".
    /// Template variables (Branch, 1.2.3) are treated as opaque suffixes.
    /// </param>
    /// <returns>
    /// A <see cref="TopologyClassification"/> with the inferred kind and exemplar name.
    /// Returns Unknown when insufficient signals are present.
    /// </returns>
    public TopologyClassification Classify(IEnumerable<string> branchPatterns)
    {
        var patterns = branchPatterns.ToList();

        var hasPrimary = patterns.Any(p => PrimaryBranch.IsMatch(p));
        var hasDevelop = patterns.Any(p => DevelopBranch.IsMatch(p));
        var hasRelease = patterns.Any(p => ReleaseBranch.IsMatch(p));
        var hasFeature = patterns.Any(p => FeatureBranch.IsMatch(p));

        if (!hasPrimary && !hasDevelop && !hasRelease && !hasFeature)
            return Unknown;

        if (hasDevelop && hasRelease)
            return CommonTopologies.GitFlow;

        // TrunkBased has no release branches — releases happen via tags
        if (hasPrimary && hasFeature && !hasDevelop && !hasRelease)
            return CommonTopologies.TrunkBased;

        if (hasDevelop || hasRelease)
            return Hybrid;

        if (hasPrimary && !hasFeature)
            return Unknown;

        return Unknown;
    }
}
