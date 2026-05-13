using System.Text.RegularExpressions;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Translates a successful <see cref="SynthesisDetectionResult"/> into an explicit
/// <see cref="SynthesisConfig"/> containing only fields the mapper chose.
/// </summary>
/// <remarks>
/// DEC-014 invariants enforced here:
///   Injective: one SynthesisInput → one SynthesisBranchConfig.
///   Explicit-only: only fields derived from intent appear in output.
///   No default inheritance: caller receives nothing it wasn't given.
/// </remarks>
public sealed class SemanticMapper
{
    private static readonly Regex VersionInName = new(@"\d+\.\d+\.\d+", RegexOptions.Compiled);
    private static readonly Regex VariablePart = new(@"/(?<name>[^/]+)$", RegexOptions.Compiled);

    private static readonly IReadOnlyList<string> GitFlowStrategies =
    [
        "Fallback", "ConfiguredNextVersion", "MergeMessage",
        "TaggedCommit", "TrackReleaseBranches", "VersionInBranchName"
    ];

    private static readonly IReadOnlyList<string> TrunkBasedStrategies =
    ["ConfiguredNextVersion", "Mainline"];

    /// <summary>
    /// Maps a successful detection result to an explicit config.
    /// </summary>
    /// <param name="detection">Must have <see cref="SynthesisDetectionResult.IsSuccessful"/> = true.</param>
    /// <param name="incrementSource">
    /// The Step-0 forced-choice intake answer for how versions advance (commits,
    /// merges, branch-name authority, or tags). Stored on the returned config but
    /// not yet consumed by the emission stage — intentional groundwork awaiting
    /// the synthesis emission iteration. Treat as a contract input, not dead state.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when detection is not successful — callers must not proceed past failed detection.
    /// </exception>
    public SynthesisConfig Map(SynthesisDetectionResult detection, IncrementSource incrementSource)
    {
        if (!detection.IsSuccessful)
            throw new InvalidOperationException(
                "SemanticMapper requires a successful detection result. " +
                "Resolve all diagnostics before mapping.");

        var branches = detection.Inputs
            .Where(i => i.Inference != null)
            .Select(i => MapBranch(i, detection))
            .ToList();

        var strategies = detection.Topology.Kind == TopologyKind.TrunkBased
            ? TrunkBasedStrategies
            : GitFlowStrategies;

        return new SynthesisConfig(detection.Topology, branches, incrementSource, strategies);
    }

    private static SynthesisBranchConfig MapBranch(SynthesisInput input, SynthesisDetectionResult detection)
    {
        var inference = input.Inference!;
        var regex = DeriveRegex(input.BranchPattern, inference);
        var sourceBranches = DeriveSourceBranches(input.BranchPattern, inference.Role, detection);

        return new SynthesisBranchConfig(
            input.BranchPattern,
            regex,
            inference.Role,
            inference.Label,
            inference.SuggestedMode ?? DeploymentMode.ContinuousDeployment,
            sourceBranches);
    }

    private static string DeriveRegex(string branchPattern, VersionExampleInference inference)
    {
        // Primary branch: fixed-name match
        if (inference.Role == BranchRole.Primary)
            return $"^{Regex.Escape(branchPattern)}$";

        var variable = VariablePart.Match(branchPattern);

        if (inference.Role == BranchRole.VersionAuthority && variable.Success)
        {
            var prefix = branchPattern[..branchPattern.LastIndexOf('/')];
            // Version authority: capture group must match semantic version pattern
            return $@"^{Regex.Escape(prefix)}/(?<BranchName>\d+\.\d+\.\d+)$";
        }

        if (variable.Success)
        {
            var prefix = branchPattern[..branchPattern.LastIndexOf('/')];
            // Label carrier with variable: capture group for BranchName substitution
            return $"^{Regex.Escape(prefix)}/(?<BranchName>.+)";
        }

        // Bare type name (e.g. "develop") — pattern matches the name directly
        return $"^{Regex.Escape(branchPattern)}$";
    }

    private static IReadOnlyList<string> DeriveSourceBranches(
        string branchPattern,
        BranchRole role,
        SynthesisDetectionResult detection)
    {
        // Primary branches are absolute roots — no source branches
        if (role == BranchRole.Primary)
            return [];

        // Find the primary branch pattern from the detection result
        var primaryPatterns = detection.Inputs
            .Where(i => i.Inference?.Role == BranchRole.Primary)
            .Select(i => i.BranchPattern)
            .ToList();

        // Hotfix-like branches (Patch increment, carrier) → source from primary only
        if (branchPattern.StartsWith("hotfix", StringComparison.OrdinalIgnoreCase))
            return primaryPatterns;

        // Release branches → source from develop-like branches
        if (role == BranchRole.VersionAuthority)
        {
            return detection.Inputs
                .Where(i => i.BranchPattern.StartsWith("dev", StringComparison.OrdinalIgnoreCase))
                .Select(i => i.BranchPattern)
                .ToList();
        }

        // Feature/bugfix → source from develop-like branches
        return detection.Inputs
            .Where(i => i.BranchPattern.StartsWith("dev", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.BranchPattern)
            .ToList();
    }
}
