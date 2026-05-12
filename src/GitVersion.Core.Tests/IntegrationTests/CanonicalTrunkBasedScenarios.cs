using GitVersion.Configuration;
using GitVersion.Testing.Extensions;

namespace GitVersion.Core.Tests.IntegrationTests;

/// <summary>
/// Executable specification for ADR-002: Canonical Trunk-Based Configuration for GitVersion.
/// Exemplar #2 — validates the abstraction seam against GitFlow (Exemplar #1).
///
/// Branch semantics:
///   main/master → every merge IS a release (ContinuousDeployment, Mainline strategy)
///                 tagged:        X.Y.Z
///                 after patch:   X.Y.(Z+1)   ← no prerelease, no metadata
///                 after minor:   X.(Y+1).0
///   feature/*   → X.(Y+1).0-{name}.N   (Minor increment, ContinuousDelivery, N per commit)
///   hotfix/*    → X.Y.(Z+1)-{name}.N   (Patch increment, ContinuousDelivery)
///
/// Structural differences from GitFlow (Exemplar #1 — what does NOT generalise):
///   - No develop branch, no release branch — simpler two-tier topology
///   - main uses ContinuousDeployment (not ManualDeployment): no prerelease on main ever
///   - feature uses Minor increment (not Inherit): bump is known at branch time, not at merge time
///   - ContinuousDelivery prerelease format: label.N (not label.1+N as in ManualDeployment)
///   - Strategy: Mainline (not TaggedCommit + TrackReleaseBranches)
///
/// What DOES generalise across both workflows:
///   - Main/master as the primary branch (no prerelease when clean)
///   - Feature and hotfix as work branches with {BranchName} label
///   - Tags always produce exact clean version
///   - +semver: commit messages for force-bump
/// </summary>
[TestFixture]
public class CanonicalTrunkBasedScenarios
{
    private static readonly IGitVersionConfiguration Configuration =
        TrunkBasedConfigurationBuilder.New.Build();

    [Test]
    public void Main_TaggedCommit_ProducesCleanVersion()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);
    }

    [Test]
    public void Main_DirectPatchCommit_IncrementsWithoutPrerelease()
    {
        // ContinuousDeployment: every commit to main IS a release — no prerelease label
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.2", Configuration);
    }

    [Test]
    public void Main_MergeFeatureBranch_IncrementsMinor()
    {
        // Merging a Minor-increment feature branch promotes main to next minor
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("feature/my-feature");
        fixture.MakeACommit();
        fixture.Checkout("master");
        fixture.MergeNoFF("feature/my-feature");
        fixture.Remove("feature/my-feature");

        fixture.AssertFullSemver("1.1.0", Configuration);
    }

    [Test]
    public void Main_MergeHotfixBranch_IncrementsPatch()
    {
        // Merging a Patch-increment hotfix branch promotes main to next patch
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("hotfix/fix-auth");
        fixture.MakeACommit();
        fixture.Checkout("master");
        fixture.MergeNoFF("hotfix/fix-auth");
        fixture.Remove("hotfix/fix-auth");

        fixture.AssertFullSemver("1.0.1", Configuration);
    }

    [Test]
    public void FeatureBranch_ProducesMinorPrerelease_WithContinuousDeliveryFormat()
    {
        // ContinuousDelivery format: label.N (N increments per commit; no +M build metadata)
        // Contrast with GitFlow ManualDeployment: label.1+N
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        // ContinuousDelivery: N=0 before any branch commit; N increments per commit
        // Contrast GitFlow ManualDeployment: label.1+N (prerelease stays .1, N is build metadata)
        fixture.BranchTo("feature/my-feature");
        fixture.AssertFullSemver("1.1.0-my-feature.0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-feature.1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-feature.2", Configuration);
    }

    [Test]
    public void HotfixBranch_ProducesPatchPrerelease_WithContinuousDeliveryFormat()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("hotfix/fix-auth");
        fixture.AssertFullSemver("1.0.1-fix-auth.0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-fix-auth.1", Configuration);
    }

    [Test]
    public void ForceBump_Major_ViaCommitMessage()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("feature/big-change");
        fixture.MakeACommit("+semver: major");
        fixture.AssertFullSemver("2.0.0-big-change.1", Configuration);
    }

    [Test]
    public void ForceBump_Minor_OnHotfix_OverridesPatchDefault()
    {
        // +semver: minor on a Patch-configured hotfix upgrades to Minor
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");

        fixture.BranchTo("hotfix/breaking-hotfix");
        fixture.MakeACommit("+semver: minor");
        fixture.AssertFullSemver("1.1.0-breaking-hotfix.1", Configuration);
    }

    [Test]
    public void EndToEnd_FullTrunkBasedLifecycle()
    {
        using var fixture = new EmptyRepositoryFixture("master");

        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);

        // Feature: minor bump — N=0 before commit, N=1 after first commit
        fixture.BranchTo("feature/login");
        fixture.AssertFullSemver("1.1.0-login.0", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-login.1", Configuration);
        fixture.Checkout("master");
        fixture.MergeNoFF("feature/login");
        fixture.Remove("feature/login");
        fixture.AssertFullSemver("1.1.0", Configuration);

        // Hotfix: patch bump on top of 1.1.0
        fixture.BranchTo("hotfix/sec-patch");
        fixture.AssertFullSemver("1.1.1-sec-patch.0", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.1-sec-patch.1", Configuration);
        fixture.Checkout("master");
        fixture.MergeNoFF("hotfix/sec-patch");
        fixture.Remove("hotfix/sec-patch");
        fixture.AssertFullSemver("1.1.1", Configuration);

        // Tag to mark a release milestone
        fixture.ApplyTag("1.1.1");
        fixture.AssertFullSemver("1.1.1", Configuration);

        // Next feature after explicit tag
        fixture.BranchTo("feature/dashboard");
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.2.0-dashboard.1", Configuration);
        fixture.Checkout("master");
        fixture.MergeNoFF("feature/dashboard");
        fixture.Remove("feature/dashboard");
        fixture.AssertFullSemver("1.2.0", Configuration);
    }

    // Self-contained GitVersion.yml for trunk-based development.
    // Based on workflow: TrunkBased/preview1 (docs/input/docs/workflows/TrunkBased/preview1.yml).
    // No customisation needed — the preview1 defaults are the canonical trunk-based config.
    public const string CanonicalTrunkBasedYaml = """
        assembly-versioning-scheme: MajorMinorPatch
        assembly-file-versioning-scheme: MajorMinorPatch
        tag-prefix: '[vV]?'
        tag-pre-release-weight: 60000
        commit-date-format: yyyy-MM-dd
        semantic-version-format: Strict
        mode: ContinuousDelivery
        label: '{BranchName}'
        increment: Inherit
        prevent-increment:
          of-merged-branch: false
          when-branch-merged: false
          when-current-commit-tagged: true
        track-merge-target: false
        track-merge-message: true
        commit-message-incrementing: Enabled
        strategies:
          - ConfiguredNextVersion
          - Mainline

        branches:
          main:
            mode: ContinuousDeployment
            label: ''
            increment: Patch
            regex: '^master$|^main$'
            source-branches: []
            is-main-branch: true
            is-release-branch: false
            prevent-increment:
              of-merged-branch: true
            pre-release-weight: 55000

          feature:
            mode: ContinuousDelivery
            label: '{BranchName}'
            increment: Minor
            regex: '^features?[/-](?<BranchName>.+)'
            source-branches:
              - main
            is-main-branch: false
            is-release-branch: false
            prevent-increment:
              when-current-commit-tagged: false
            pre-release-weight: 30000

          hotfix:
            mode: ContinuousDelivery
            label: '{BranchName}'
            increment: Patch
            regex: '^hotfix(es)?[/-](?<BranchName>.+)'
            source-branches:
              - main
            is-main-branch: false
            is-release-branch: true
            prevent-increment:
              when-current-commit-tagged: false
            pre-release-weight: 30000

          pull-request:
            mode: ContinuousDelivery
            label: 'PullRequest{Number}'
            increment: Inherit
            regex: '^(pull-requests?|pull|pr)[/-](?<Number>\d*)'
            source-branches:
              - main
              - feature
              - hotfix
            is-main-branch: false
            is-release-branch: false
            prevent-increment:
              of-merged-branch: true
              when-current-commit-tagged: false
            pre-release-weight: 30000

          unknown:
            increment: Patch
            regex: '(?<BranchName>.+)'
            source-branches:
              - main
            is-main-branch: false
            is-release-branch: false
            prevent-increment:
              when-current-commit-tagged: false
            pre-release-weight: 30000
        """;
}
