using System.Text.RegularExpressions;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Parses a (branchPattern, versionExample) pair into a <see cref="VersionExampleInference"/>.
/// </summary>
/// <remarks>
/// Pure function — no YAML emission, no Git access.
///
/// Label derivation rule:
///   If the prerelease segment starts with the variable part of the branch pattern
///   (the segment after the final '/'), the label is {BranchName}.
///   Otherwise the label is the static string before the commit counter.
///
/// Mode detection from separator style:
///   "alpha1243"   → ContinuousDeployment  (concatenated)
///   "alpha.1243"  → ContinuousDelivery    (dot-separated)
///   "alpha.1+42"  → ManualDeployment      (prerelease.counter+metadata)
///
/// Version authority detection:
///   When the branch pattern's variable segment is a semantic version (x.y.z)
///   the branch is a Version Authority — it derives its version from the branch name.
/// </remarks>
public sealed class VersionExampleParser
{
    private static readonly Regex SemVerCorePattern = new(@"^\d+\.\d+\.\d+", RegexOptions.Compiled);
    private static readonly Regex PrereleasePattern = new(@"^[^-]+-(?<prerelease>.+)$", RegexOptions.Compiled);
    private static readonly Regex ConcatenatedPrerelease = new(@"^(?<label>[A-Za-z]+)(?<n>\d+)$", RegexOptions.Compiled);
    private static readonly Regex DotSeparatedPrerelease = new(@"^(?<label>[A-Za-z][A-Za-z0-9\-]*)\.(?<n>\d+)$", RegexOptions.Compiled);
    private static readonly Regex ManualDeploymentPrerelease = new(@"^(?<label>[A-Za-z][A-Za-z0-9\-]*)\.(?<n>\d+)\+(?<meta>\d+)$", RegexOptions.Compiled);

    private static readonly VersionExampleInference PrimaryInference =
        new(BranchRole.Primary, string.Empty, null);

    /// <summary>
    /// Infers label, role, and suggested deployment mode from a single example pair.
    /// </summary>
    /// <param name="branchPattern">
    /// Branch name pattern as the user supplied it, e.g. "develop", "feature/Login", "release/1.62.0".
    /// </param>
    /// <param name="versionExample">
    /// An example version string for that branch, e.g. "1.62.0-alpha1243", "1.62.0".
    /// </param>
    public VersionExampleInference Parse(string branchPattern, string versionExample)
    {
        var prereleaseMatch = PrereleasePattern.Match(versionExample);
        if (!prereleaseMatch.Success)
            return PrimaryInference;

        var prerelease = prereleaseMatch.Groups["prerelease"].Value;
        var role = InferRole(branchPattern, versionExample);
        var (label, mode) = InferLabelAndMode(branchPattern, prerelease);

        return new VersionExampleInference(role, label, mode);
    }

    private static BranchRole InferRole(string branchPattern, string versionExample)
    {
        var variablePart = ExtractVariablePart(branchPattern);

        if (variablePart != null && SemVerCorePattern.IsMatch(variablePart))
        {
            var coreVersion = SemVerCorePattern.Match(versionExample).Value;
            if (variablePart.StartsWith(coreVersion, StringComparison.Ordinal))
                return BranchRole.VersionAuthority;
        }

        return BranchRole.LabelCarrier;
    }

    private static (string label, DeploymentMode? mode) InferLabelAndMode(string branchPattern, string prerelease)
    {
        var manual = ManualDeploymentPrerelease.Match(prerelease);
        if (manual.Success)
            return (ResolveLabel(branchPattern, manual.Groups["label"].Value), DeploymentMode.ManualDeployment);

        var dotSep = DotSeparatedPrerelease.Match(prerelease);
        if (dotSep.Success)
            return (ResolveLabel(branchPattern, dotSep.Groups["label"].Value), DeploymentMode.ContinuousDelivery);

        var concat = ConcatenatedPrerelease.Match(prerelease);
        if (concat.Success)
            return (ResolveLabel(branchPattern, concat.Groups["label"].Value), DeploymentMode.ContinuousDeployment);

        // Format not recognised — mode cannot be inferred; ambiguity detector will emit F-002
        return (ResolveLabel(branchPattern, prerelease), null);
    }

    private static string ResolveLabel(string branchPattern, string candidateLabel)
    {
        var variablePart = ExtractVariablePart(branchPattern);

        if (variablePart != null
            && !SemVerCorePattern.IsMatch(variablePart)
            && candidateLabel.Equals(variablePart, StringComparison.OrdinalIgnoreCase))
            return "{BranchName}";

        return candidateLabel;
    }

    private static string? ExtractVariablePart(string branchPattern)
    {
        var slash = branchPattern.LastIndexOf('/');
        return slash >= 0 ? branchPattern[(slash + 1)..] : null;
    }
}
