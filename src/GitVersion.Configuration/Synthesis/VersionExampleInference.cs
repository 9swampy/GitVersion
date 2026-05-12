using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Synthesis;

public enum BranchRole { Primary, VersionAuthority, LabelCarrier }

/// <summary>
/// The result of parsing a single (branchPattern, versionExample) pair.
/// Represents what can be inferred from one example — may be under-determined
/// when used in isolation; the ambiguity detector (Task 4) handles that.
/// </summary>
/// <param name="Role">Whether the branch defines versions, carries labels, or is the primary release line.</param>
/// <param name="Label">
/// The inferred label: empty string for primary, static label ("alpha", "beta"),
/// or "{BranchName}" when the label is derived from the branch name variable.
/// </param>
/// <param name="SuggestedMode">
/// The deployment mode implied by the separator style in the version example.
/// Null when the example carries no prerelease (primary branch).
/// </param>
public sealed record VersionExampleInference(
    BranchRole Role,
    string Label,
    DeploymentMode? SuggestedMode);
