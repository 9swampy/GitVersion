using System.Text.RegularExpressions;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Validation;

/// <summary>
/// Validates an IGitVersionConfiguration against the workflow-agnostic semantic rule catalogue.
/// Operates purely on configuration fields — no GitVersion calculation engine required.
/// </summary>
/// <remarks>
/// Rules: SEM-001 through SEM-007 (see .ai/semantic-rules/SEM-RULES-CATALOGUE.md).
/// Physics: invariants the version graph must satisfy regardless of workflow.
/// Policy: GitFlow, TrunkBased, etc. — out of scope for this validator.
/// </remarks>
public sealed class ConfigurationSemanticValidator
{
    private static readonly Regex VersionPattern = new(@"\d+\.\d+", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex CaptureGroupPattern = new(@"\(\?<(\w+)>", RegexOptions.Compiled);

    public IReadOnlyList<SemanticViolation> Validate(IGitVersionConfiguration configuration)
    {
        var violations = new List<SemanticViolation>();

        var branches = configuration.Branches;

        foreach (var (branchName, branch) in branches)
        {
            CheckSem001(violations, branchName, branch);
            CheckSem002(violations, branchName, branch);
            CheckSem003(violations, branchName, branch);
            CheckSem004(violations, branchName, branch, configuration);
            CheckSem005(violations, branchName, branch, branches);
            CheckSem007(violations, branchName, branch);
        }

        CheckSem006(violations, configuration);

        return violations.AsReadOnly();
    }

    // SEM-001: Authority/Carrier Exclusivity
    // A branch may not simultaneously claim version authority and calculate an increment.
    private static void CheckSem001(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch)
    {
        // Incompatible flag combination: both main and release branch
        if (branch.IsMainBranch == true && branch.IsReleaseBranch == true)
        {
            violations.Add(new SemanticViolation(
                "SEM-001",
                SemanticViolationSeverity.Error,
                branchName,
                $"Branch '{branchName}' declares both is-main-branch: true and is-release-branch: true. " +
                "These are mutually exclusive authority roles.",
                "Set is-main-branch: true and is-release-branch: false. " +
                "Primary branches derive version from tags, not from branch name."));
        }

        // Release branch with no version-parseable regex is an unfulfillable authority claim
        if (branch.IsReleaseBranch == true
            && !string.IsNullOrEmpty(branch.RegularExpression)
            && !VersionPattern.IsMatch(branch.RegularExpression))
        {
            violations.Add(new SemanticViolation(
                "SEM-001",
                SemanticViolationSeverity.Error,
                branchName,
                $"Branch '{branchName}' declares is-release-branch: true but its regex '{branch.RegularExpression}' " +
                "cannot match a version string (no \\d+.\\d+ pattern). " +
                "VersionInBranchName strategy will fail to parse the branch name.",
                "Either change the regex to match a version pattern (e.g. releases?[/-](?<BranchName>.+)) " +
                "or set is-release-branch: false."));
        }
    }

    // SEM-002: Primary Branch Lineage Isolation
    // A primary branch must declare source-branches: [] to prevent version inheritance from ancestors.
    private static void CheckSem002(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch)
    {
        if (branch.IsMainBranch == true && branch.SourceBranches.Count != 0)
        {
            violations.Add(new SemanticViolation(
                "SEM-002",
                SemanticViolationSeverity.Error,
                branchName,
                $"Branch '{branchName}' is a primary branch (is-main-branch: true) but declares " +
                $"source-branches: [{string.Join(", ", branch.SourceBranches)}]. " +
                "Without source-branches: [], GitVersion will traverse merge commit parents through " +
                "ancestor branches, inheriting their prerelease labels and commit counts.",
                "Set source-branches: [] on all is-main-branch: true branches. " +
                "This declares the branch as an absolute root in the version graph."));
        }
    }

    // SEM-003: Variable Capture Contract
    // A {BranchName} (or similar) label requires a matching named capture group in the regex.
    private static void CheckSem003(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch)
    {
        if (string.IsNullOrEmpty(branch.Label))
            return;

        var placeholders = PlaceholderPattern.Matches(branch.Label);
        if (placeholders.Count == 0)
            return;

        var captureGroups = CaptureGroupPattern.Matches(branch.RegularExpression ?? string.Empty)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Match placeholder in placeholders)
        {
            var name = placeholder.Groups[1].Value;
            if (name == "Number") continue; // PullRequest{Number} is handled internally
            if (!captureGroups.Contains(name))
            {
                violations.Add(new SemanticViolation(
                    "SEM-003",
                    SemanticViolationSeverity.Error,
                    branchName,
                    $"Branch '{branchName}' uses label template '{{{name}}}' but its regex " +
                    $"'{branch.RegularExpression}' contains no named capture group (?<{name}>...). " +
                    $"The literal string '{{{name}}}' will be emitted instead of the branch name segment.",
                    $"Add a named capture group to the regex, e.g. (?<{name}>.+), " +
                    "or change the label to a static string."));
            }
        }
    }

    // SEM-004: Deployment Mode Consistency
    // Root ContinuousDeployment + labelled branches with no explicit mode = silent label suppression.
    private static void CheckSem004(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch, IGitVersionConfiguration configuration)
    {
        if (configuration.DeploymentMode != DeploymentMode.ContinuousDeployment)
            return;

        if (string.IsNullOrEmpty(branch.Label))
            return;

        // Branch has a non-empty label but no explicit mode override — will inherit ContinuousDeployment
        if (branch.DeploymentMode == null)
        {
            violations.Add(new SemanticViolation(
                "SEM-004",
                SemanticViolationSeverity.Warning,
                branchName,
                $"Branch '{branchName}' has label '{branch.Label}' but no explicit mode override. " +
                "The root mode: ContinuousDeployment will suppress this prerelease label, " +
                "making all commits on this branch appear as clean release versions.",
                "Either set root mode: ContinuousDelivery, or add an explicit " +
                "mode: ContinuousDelivery (or ManualDeployment) to this branch configuration."));
        }
    }

    // SEM-005: Source Branch Reference Integrity
    // Every source-branches entry must name an existing branch key.
    private static void CheckSem005(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch, IReadOnlyDictionary<string, IBranchConfiguration> branches)
    {
        foreach (var sourceBranch in branch.SourceBranches)
        {
            if (!branches.ContainsKey(sourceBranch))
            {
                violations.Add(new SemanticViolation(
                    "SEM-005",
                    SemanticViolationSeverity.Error,
                    branchName,
                    $"Branch '{branchName}' declares source-branches entry '{sourceBranch}' " +
                    "which is not defined in the branches configuration.",
                    $"Either define a '{sourceBranch}' branch in the configuration, " +
                    $"or remove it from '{branchName}'.source-branches."));
            }
        }
    }

    // SEM-006: Strategies Must Be Declared
    // Implicit strategy composition is fragile across GitVersion version upgrades.
    private static void CheckSem006(List<SemanticViolation> violations, IGitVersionConfiguration configuration)
    {
        if (configuration.VersionStrategy == VersionStrategies.None)
        {
            violations.Add(new SemanticViolation(
                "SEM-006",
                SemanticViolationSeverity.Warning,
                null,
                "No strategies block is declared. GitVersion's default strategy composition " +
                "has changed across major versions and may silently change behaviour on upgrades.",
                "Explicitly declare the strategies list. " +
                "For GitFlow: [Fallback, ConfiguredNextVersion, MergeMessage, TaggedCommit, TrackReleaseBranches, VersionInBranchName]. " +
                "For TrunkBased: [ConfiguredNextVersion, Mainline]."));
        }
    }

    // SEM-007: Increment Strategy Totality
    // increment: Inherit requires at least one source branch to inherit from.
    private static void CheckSem007(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch)
    {
        if (branch.Increment == IncrementStrategy.Inherit && branch.SourceBranches.Count == 0)
        {
            violations.Add(new SemanticViolation(
                "SEM-007",
                SemanticViolationSeverity.Warning,
                branchName,
                $"Branch '{branchName}' uses increment: Inherit but declares no source-branches. " +
                "There is no parent context to inherit from; GitVersion will fall back to " +
                "its internal defaults, which may change across versions.",
                "Add at least one entry to source-branches, e.g. source-branches: [main]."));
        }
    }
}
