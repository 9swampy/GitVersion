using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// A branch configuration produced by SemanticMapper.
/// Contains only fields explicitly chosen by the mapper — no inherited defaults.
/// </summary>
/// <param name="BranchPattern">The user-supplied branch pattern, e.g. "feature/Login".</param>
/// <param name="DerivedRegex">The GitVersion regex derived from the pattern, e.g. "^feature/(?&lt;BranchName&gt;.+)".</param>
/// <param name="Role">Primary, VersionAuthority, or LabelCarrier.</param>
/// <param name="Label">Explicit label string. Empty for primary, static ("alpha") or {BranchName}.</param>
/// <param name="Mode">Deployment mode derived from the version example's separator style.</param>
/// <param name="SourceBranches">Explicit source-branches declaration. Empty list for primary branches.</param>
public sealed record SynthesisBranchConfig(
    string BranchPattern,
    string DerivedRegex,
    BranchRole Role,
    string Label,
    DeploymentMode Mode,
    IReadOnlyList<string> SourceBranches);

/// <summary>
/// The complete explicit configuration produced by SemanticMapper.
/// Every field was set deliberately — nothing was inherited from a template.
/// </summary>
/// <param name="Topology">The classified topology from Step 1.</param>
/// <param name="Branches">One <see cref="SynthesisBranchConfig"/> per input — injective mapping.</param>
/// <param name="IncrementSource">How the version number advances — the forced-choice from intake.</param>
/// <param name="Strategies">GitVersion version strategies for the topology.</param>
public sealed record SynthesisConfig(
    TopologyClassification Topology,
    IReadOnlyList<SynthesisBranchConfig> Branches,
    IncrementSource IncrementSource,
    IReadOnlyList<string> Strategies);
