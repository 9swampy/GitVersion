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
    public string Emit(SynthesisConfig config)
    {
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
