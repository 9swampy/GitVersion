using GitVersion.Configuration;
using GitVersion.Testing.Extensions;
using GitVersion.VersionCalculation;
using LibGit2Sharp;

namespace GitVersion.Core.Tests.IntegrationTests;

/// <summary>
/// Executable specification for ADR-001: Canonical GitFlow Configuration for GitVersion.
///
/// Branch semantics (Version Authority vs. Label Carrier model):
///   master/main   → Version Authority (primary release line)
///                   tagged:         X.Y.Z
///                   between tags:   X.Y.(Z+1)-1+N  [ADR constraint: 1.0.0+N is unachievable in GitVersion]
///   develop       → Label Carrier,  alpha, Minor increment
///                   X.Y.0-alpha.N
///   release/X.Y.Z → Version Authority (branch name drives version)
///                   X.Y.Z-beta.1+N
///   feature/*     → Label Carrier,  {BranchName}, Inherit increment
///                   X.Y.0-{name}.1+N
///   bugfix/*      → Label Carrier,  {BranchName}, Inherit increment  [not in default GitFlow]
///                   X.Y.0-{name}.1+N
///   hotfix/*      → Label Carrier,  {BranchName}, Inherit increment  [NOT "beta"]
///                   X.Y.(Z+1)-{name}.1+N
///
/// Increment mechanisms:
///   Git tag        → exact version, resets all counters
///   +semver: major → force major bump from any branch
///   +semver: minor → force minor bump from any branch
///   +semver: patch → force patch bump, overrides branch default
///   release/X.Y.Z  → explicit version via branch name (VersionInBranchName strategy)
/// </summary>
[TestFixture]
public class CanonicalGitFlowScenarios
{
    private static readonly IGitVersionConfiguration Configuration =
        GitFlowConfigurationBuilder.New
            // master: ManualDeployment — produces X.Y.(Z+1)-1+N between tags, X.Y.Z when tagged
            .WithBranch(ConfigurationConstants.MainBranchKey, b => b
                .WithDeploymentMode(DeploymentMode.ManualDeployment))
            // hotfix: branch name as label (not "beta"), not a release branch
            .WithBranch(ConfigurationConstants.HotfixBranchKey, b => b
                .WithLabel(ConfigurationConstants.BranchNamePlaceholder)
                .WithIsReleaseBranch(false))
            // bugfix: new branch type — mirrors feature structure
            .WithBranch("bugfix", b => b
                .WithIncrement(IncrementStrategy.Inherit)
                .WithRegularExpression(@"^bugfix[s]?[/-](?<BranchName>.+)")
                .WithDeploymentMode(DeploymentMode.ManualDeployment)
                .WithLabel(ConfigurationConstants.BranchNamePlaceholder)
                .WithPreventIncrementWhenCurrentCommitTagged(false)
                .WithSourceBranches(
                    ConfigurationConstants.DevelopBranchKey,
                    ConfigurationConstants.MainBranchKey,
                    ConfigurationConstants.ReleaseBranchKey,
                    ConfigurationConstants.SupportBranchKey,
                    ConfigurationConstants.HotfixBranchKey))
            .Build();

    [Test]
    public void Master_TaggedCommit_ProducesCleanVersion()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);

        fixture.MakeATaggedCommit("2.0.0");
        fixture.AssertFullSemver("2.0.0", Configuration);
    }

    [Test]
    public void Master_BetweenTags_ProducesBuildMetadata()
    {
        // ADR constraint: 1.0.0+N is not achievable in GitVersion — ManualDeployment
        // with empty label and Patch increment produces X.Y.(Z+1)-1+N between tags.
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-1+2", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-1+3", Configuration);

        fixture.ApplyTag("1.0.1");
        fixture.AssertFullSemver("1.0.1", Configuration);
    }

    [Test]
    public void Develop_ProducesAlphaVersion()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("develop");
        fixture.AssertFullSemver("1.1.0-alpha.0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-alpha.1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-alpha.2", Configuration);
    }

    [Test]
    public void FeatureBranch_ProducesLabelFromBranchName()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("develop");
        fixture.MakeACommit();

        fixture.BranchTo("feature/my-feature");
        fixture.AssertFullSemver("1.1.0-my-feature.1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-feature.1+2", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-feature.1+3", Configuration);
    }

    [Test]
    public void BugfixBranch_ProducesLabelFromBranchName()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("develop");
        fixture.MakeACommit();

        fixture.BranchTo("bugfix/my-fix");
        fixture.AssertFullSemver("1.1.0-my-fix.1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-fix.1+2", Configuration);
    }

    [Test]
    public void ReleaseBranch_ProducesBetaWithVersionFromBranchName()
    {
        // release/0.1.0 demonstrates minor bump from 0.0.1.
        // Version is driven by the branch name via VersionInBranchName strategy (IsReleaseBranch = true).
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("0.0.1");

        fixture.BranchTo("develop");
        fixture.MakeACommit();

        fixture.BranchTo("release/0.1.0");
        fixture.AssertFullSemver("0.1.0-beta.1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("0.1.0-beta.1+2", Configuration);

        fixture.Checkout("master");
        fixture.MergeNoFF("release/0.1.0");
        fixture.ApplyTag("0.1.0");
        fixture.AssertFullSemver("0.1.0", Configuration);
    }

    [Test]
    public void HotfixBranch_ProducesLabelFromBranchName_NotBeta()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("hotfix/fix-auth");
        fixture.AssertFullSemver("1.0.1-fix-auth.1+0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-fix-auth.1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-fix-auth.1+2", Configuration);
    }

    [Test]
    public void ForceBump_Major_ViaCommitMessage()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("develop");
        fixture.MakeACommit("+semver: major");
        fixture.AssertFullSemver("2.0.0-alpha.1", Configuration);
    }

    [Test]
    public void ForceBump_Patch_OnMinorBranch_DoesNotDowngradeIncrement()
    {
        // +semver: patch says "at least patch" — develop's Minor increment is higher and wins.
        // The commit message does not downgrade the branch's configured increment.
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.2.0");

        fixture.BranchTo("develop");
        fixture.MakeACommit("+semver: patch");
        fixture.AssertFullSemver("1.3.0-alpha.1", Configuration);
    }

    [Test]
    public void EndToEnd_FullGitFlowLifecycle()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);

        fixture.BranchTo("develop");
        fixture.AssertFullSemver("1.1.0-alpha.0", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-alpha.1", Configuration);

        // +N counts parent branch commits since version source, not only current branch commits
        fixture.BranchTo("feature/login");
        fixture.AssertFullSemver("1.1.0-login.1+1", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-login.1+2", Configuration);
        fixture.Checkout("develop");
        fixture.MergeNoFF("feature/login");
        fixture.Remove("feature/login");
        // no-ff: develop commit + feature commit + merge commit = 3
        fixture.AssertFullSemver("1.1.0-alpha.3", Configuration);

        fixture.BranchTo("bugfix/typo-fix");
        fixture.AssertFullSemver("1.1.0-typo-fix.1+3", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-typo-fix.1+4", Configuration);
        fixture.Checkout("develop");
        fixture.MergeNoFF("bugfix/typo-fix");
        fixture.Remove("bugfix/typo-fix");
        // no-ff: 3 + bugfix commit + merge commit = 5
        fixture.AssertFullSemver("1.1.0-alpha.5", Configuration);

        fixture.BranchTo("release/1.1.0");
        fixture.AssertFullSemver("1.1.0-beta.1+5", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-beta.1+6", Configuration);
        fixture.Checkout("master");
        fixture.MergeNoFF("release/1.1.0");
        fixture.ApplyTag("1.1.0");
        fixture.AssertFullSemver("1.1.0", Configuration);
        fixture.Checkout("develop");
        fixture.MergeNoFF("release/1.1.0");
        fixture.Remove("release/1.1.0");

        fixture.Checkout("master");
        fixture.BranchTo("hotfix/sec-patch");
        fixture.AssertFullSemver("1.1.1-sec-patch.1+0", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.1-sec-patch.1+1", Configuration);
        fixture.Checkout("master");
        fixture.MergeNoFF("hotfix/sec-patch");
        fixture.ApplyTag("1.1.1");
        fixture.AssertFullSemver("1.1.1", Configuration);
        fixture.Checkout("develop");
        fixture.MergeNoFF("hotfix/sec-patch");
        fixture.Remove("hotfix/sec-patch");

        fixture.Checkout("master");
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.2-1+1", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.2-1+2", Configuration);
        fixture.ApplyTag("1.1.2");
        fixture.AssertFullSemver("1.1.2", Configuration);
    }

    /// <summary>
    /// GitVersion.yml equivalent — this is the branch deliverable.
    ///
    /// Place this file at the root of any repository to apply canonical GitFlow versioning.
    ///
    /// ADR-001 constraint: master between tags produces X.Y.(Z+1)-1+N, not X.Y.Z+N.
    /// SemVer ordering requires advancing the version number past any tag.
    /// Tag a commit to reset to a clean X.Y.Z version.
    /// </summary>
    public const string CanonicalGitFlowYaml = """
        branches:
          main:
            regex: '^master$|^main$'
            label: ''
            increment: Patch
            deployment-mode: ManualDeployment
            is-main-branch: true
            prevent-increment:
              of-merged-branch: true

          develop:
            regex: '^dev(elop)?(ment)?$'
            label: alpha
            increment: Minor
            deployment-mode: ContinuousDelivery
            tracks-release-branches: true
            track-merge-target: true

          release:
            regex: '^releases?[/-](?<BranchName>.+)'
            label: beta
            increment: Minor
            deployment-mode: ManualDeployment
            is-release-branch: true
            prevent-increment:
              of-merged-branch: true
            source-branches: [main, support]

          feature:
            regex: '^features?[/-](?<BranchName>.+)'
            label: '{BranchName}'
            increment: Inherit
            deployment-mode: ManualDeployment
            source-branches: [develop, main, release, support, hotfix]

          bugfix:
            regex: '^bugfix[s]?[/-](?<BranchName>.+)'
            label: '{BranchName}'
            increment: Inherit
            deployment-mode: ManualDeployment
            source-branches: [develop, main, release, support, hotfix]

          hotfix:
            regex: '^hotfix(es)?[/-](?<BranchName>.+)'
            label: '{BranchName}'
            increment: Inherit
            deployment-mode: ManualDeployment
            is-release-branch: false
            source-branches: [main, support]

          support:
            regex: '^support[/-](?<BranchName>.+)'
            label: ''
            increment: Patch
            is-main-branch: true
            prevent-increment:
              of-merged-branch: true
            source-branches: [main]

        commit-message-incrementing: Enabled
        """;
}
