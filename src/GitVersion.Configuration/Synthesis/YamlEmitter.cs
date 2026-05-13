using System.Text;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Serializes a <see cref="SynthesisConfig"/> to a minimal <c>GitVersion.yml</c> string.
/// Emits only fields explicitly set by <see cref="SemanticMapper"/> — no default inheritance.
/// </summary>
/// <remarks>
/// Structural scaffolding (assembly-versioning-scheme, commit-date-format, etc.) is included
/// as format requirements for ConfigurationSerializer, not as semantic choices.
/// Semantic fields (mode, label, regex, strategies, role flags) come entirely from the config.
/// </remarks>
public sealed class YamlEmitter
{
    /// <summary>
    /// Produces a minimal, self-contained <c>GitVersion.yml</c> string from the mapped config.
    /// The output is parseable by <see cref="ConfigurationSerializer"/> and must pass
    /// <see cref="ConfigurationSemanticValidator"/> with no errors.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two or more branches in <paramref name="config"/> share a family key
    /// derived by <see cref="BranchFamilyKey"/>. This indicates a violation of the emission
    /// injectivity invariant (DEC-018) that should have been intercepted upstream by
    /// <see cref="AmbiguityDetector"/> as F-005. The emitter never recovers — no merging,
    /// no deduplication, no last-write-wins — because the user-facing diagnostic and the
    /// validator-as-oracle separation depend on the synthesis pipeline failing loudly.
    /// </exception>
    public string Emit(SynthesisConfig config)
    {
        EnsureUniqueFamilyKeys(config);

        var sb = new StringBuilder();

        // Structural scaffolding — format requirements, not semantic choices
        sb.AppendLine("assembly-versioning-scheme: MajorMinorPatch");
        sb.AppendLine("assembly-file-versioning-scheme: MajorMinorPatch");
        sb.AppendLine("tag-prefix: '[vV]?'");
        sb.AppendLine("tag-pre-release-weight: 60000");
        sb.AppendLine("commit-date-format: yyyy-MM-dd");
        sb.AppendLine("semantic-version-format: Strict");

        // Root deployment mode — from topology (ContinuousDeployment for GitFlow/TrunkBased)
        sb.AppendLine($"mode: {FormatMode(config.Topology.Kind)}");

        sb.AppendLine("commit-message-incrementing: Enabled");

        // Strategies — from SemanticMapper, required to avoid SEM-006
        EmitStrategies(sb, config.Strategies);

        sb.AppendLine();
        sb.AppendLine("branches:");

        foreach (var branch in config.Branches)
            EmitBranch(sb, branch);

        return sb.ToString();
    }

    private static void EnsureUniqueFamilyKeys(SynthesisConfig config)
    {
        var collisions = config.Branches
            .GroupBy(b => BranchFamilyKey.Derive(b.BranchPattern))
            .Where(g => g.Count() > 1)
            .ToList();

        if (collisions.Count == 0)
            return;

        var details = string.Join("; ", collisions.Select(c =>
            $"'{c.Key}' shared by [{string.Join(", ", c.Select(b => b.BranchPattern))}]"));

        throw new InvalidOperationException(
            $"Cannot emit YAML: duplicate branch family keys detected — {details}. " +
            "This violates synthesis invariants — AmbiguityDetector (F-005) is the " +
            "boundary that rejects this intake shape; reaching the emitter with " +
            "colliding family keys indicates the detector was bypassed or regressed.");
    }

    private static void EmitStrategies(StringBuilder sb, IReadOnlyList<string> strategies)
    {
        sb.Append("strategies:");
        sb.AppendLine();
        foreach (var strategy in strategies)
            sb.AppendLine($"  - {strategy}");
    }

    private static void EmitBranch(StringBuilder sb, SynthesisBranchConfig branch)
    {
        var key = BranchFamilyKey.Derive(branch.BranchPattern);
        sb.AppendLine($"  {key}:");
        sb.AppendLine($"    regex: '{EscapeRegex(branch.DerivedRegex)}'");
        sb.AppendLine($"    label: '{branch.Label}'");
        sb.AppendLine($"    mode: {branch.Mode}");
        sb.AppendLine($"    is-main-branch: {(branch.Role == BranchRole.Primary ? "true" : "false")}");
        sb.AppendLine($"    is-release-branch: {(branch.Role == BranchRole.VersionAuthority ? "true" : "false")}");

        if (branch.SourceBranches.Count == 0)
        {
            sb.AppendLine("    source-branches: []");
        }
        else
        {
            sb.AppendLine("    source-branches:");
            foreach (var src in branch.SourceBranches)
                sb.AppendLine($"      - {BranchFamilyKey.Derive(src)}");
        }
    }

    private static string EscapeRegex(string regex)
        => regex.Replace("'", "''");

    private static string FormatMode(TopologyKind kind) => kind switch
    {
        TopologyKind.TrunkBased => nameof(DeploymentMode.ContinuousDeployment),
        _ => nameof(DeploymentMode.ContinuousDeployment)
    };
}
