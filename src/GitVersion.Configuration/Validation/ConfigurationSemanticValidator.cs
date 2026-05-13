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
    private static readonly Regex PlaceholderPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex CaptureGroupPattern = new(@"\(\?<(\w+)>", RegexOptions.Compiled);
    // A release branch can yield a version if its regex contains digits directly
    // OR has a named capture group whose value GitVersion will parse at runtime.
    private static readonly Regex DigitPattern = new(@"\\d|\[.*\d.*\]", RegexOptions.Compiled);

    public IReadOnlyList<SemanticViolation> Validate(IGitVersionConfiguration configuration)
    {
        var violations = new List<SemanticViolation>();

        var branches = configuration.Branches;

        foreach (var (branchName, branch) in branches)
        {
            CheckSem001(violations, branchName, branch);
            CheckSem002(violations, branchName, branch, branches);
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
                "Authority/Carrier Exclusivity",
                SemanticViolationSeverity.Error,
                branchName,
                $"Branch '{branchName}' declares both is-main-branch: true and is-release-branch: true. " +
                "These are mutually exclusive authority roles.",
                "Set is-main-branch: true and is-release-branch: false. " +
                "Primary branches derive version from tags, not from branch name."));
        }

        // Release branch with no version-parseable regex is an unfulfillable authority claim.
        // A regex can yield a version if it either contains digit patterns directly,
        // or has a capture group whose value GitVersion will parse at runtime.
        if (branch.IsReleaseBranch == true
            && !string.IsNullOrEmpty(branch.RegularExpression)
            && !DigitPattern.IsMatch(branch.RegularExpression)
            && !CaptureGroupPattern.IsMatch(branch.RegularExpression))
        {
            violations.Add(new SemanticViolation(
                "SEM-001",
                "Authority/Carrier Exclusivity",
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
    // A primary branch must not declare source-branches containing prerelease-carrying branches,
    // as this opens the version graph to label inheritance from non-primary ancestors.
    // Note: source-branches: [main] on a support branch is legitimate — main has empty label.
    private static void CheckSem002(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch,
        IReadOnlyDictionary<string, IBranchConfiguration> branches)
    {
        if (branch.IsMainBranch != true || branch.SourceBranches.Count == 0)
            return;

        var labelledSources = branch.SourceBranches
            .Where(s => branches.TryGetValue(s, out var sourceBranch) && !string.IsNullOrEmpty(sourceBranch.Label))
            .ToList();

        if (labelledSources.Count == 0)
            return;

        violations.Add(new SemanticViolation(
            "SEM-002",
            "Primary Branch Lineage Isolation",
            SemanticViolationSeverity.Error,
            branchName,
            $"Branch '{branchName}' is a primary branch (is-main-branch: true) but declares " +
            $"source-branches containing prerelease-carrying branches: [{string.Join(", ", labelledSources)}]. " +
            "GitVersion will traverse merge commit parents through these branches, " +
            "inheriting their prerelease labels and commit counts.",
            "Remove prerelease-carrying branches from source-branches, or set source-branches: [] " +
            "to declare this branch as an absolute root in the version graph."));
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
                    "Variable Capture Contract",
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

    // SEM-004: Deployment Mode Semantics Alignment
    // Advisory: ContinuousDeployment + non-empty label = real intent tension, explained not penalised.
    // The label IS used (e.g. 1.62.0-Name123) but every commit is treated as deployable.
    // Suppressed for {BranchName} labels on obvious non-release branches — CD semantics are intentional there.
    private static void CheckSem004(List<SemanticViolation> violations, string branchName, IBranchConfiguration branch, IGitVersionConfiguration configuration)
    {
        if (configuration.DeploymentMode != DeploymentMode.ContinuousDeployment)
            return;
        if (string.IsNullOrEmpty(branch.Label))
            return;
        if (branch.DeploymentMode != null)
            return;
        if (branch.Label == ConfigurationConstants.BranchNamePlaceholder && IsShortLivedBranch(branchName))
            return;

        violations.Add(new SemanticViolation(
            "SEM-004",
            "Deployment Mode Semantics Alignment",
            SemanticViolationSeverity.Advisory,
            branchName,
            $"Branch '{branchName}' uses ContinuousDeployment mode while carrying the prerelease label '{branch.Label}'. " +
            $"The label will appear in version output (e.g. 1.0.0-{branch.Label.Replace(ConfigurationConstants.BranchNamePlaceholder, "Name")}123), " +
            "but each commit is still treated as a deployable version.",
            "If this is intentional, no action is required. " +
            "If prerelease versions should be explicitly distinguished from deployable releases, " +
            "add mode: ContinuousDelivery to this branch configuration."));
    }

    private static bool IsShortLivedBranch(string branchName) =>
        branchName.StartsWith("feature", StringComparison.OrdinalIgnoreCase) ||
        branchName.StartsWith("bugfix",  StringComparison.OrdinalIgnoreCase) ||
        branchName.StartsWith("hotfix",  StringComparison.OrdinalIgnoreCase);

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
                    "Source Branch Reference Integrity",
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
                "Strategies Must Be Declared",
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
                "Increment Strategy Totality",
                SemanticViolationSeverity.Warning,
                branchName,
                $"Branch '{branchName}' uses increment: Inherit but declares no source-branches. " +
                "There is no parent context to inherit from; GitVersion will fall back to " +
                "its internal defaults, which may change across versions.",
                "Add at least one entry to source-branches, e.g. source-branches: [main]."));
        }
    }
}
