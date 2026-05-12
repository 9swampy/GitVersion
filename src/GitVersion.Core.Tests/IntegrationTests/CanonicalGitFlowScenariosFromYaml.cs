using GitVersion.Configuration;
using GitVersion.Testing.Extensions;

namespace GitVersion.Core.Tests.IntegrationTests;

/// <summary>
/// Validates that CanonicalGitFlowYaml (the deliverable) produces identical
/// versioning behaviour to the builder-derived Configuration in CanonicalGitFlowScenarios.
///
/// These tests parse the YAML string through ConfigurationSerializer — the same code
/// path used when GitVersion reads a real GitVersion.yml file from a repository root.
/// Green here means the YAML file can be dropped into any repo and produce the
/// asserted versions on real Git histories.
/// </summary>
[TestFixture]
public class CanonicalGitFlowScenariosFromYaml
{
    private static readonly IGitVersionConfiguration Configuration =
        new ConfigurationSerializer().ReadConfiguration(CanonicalGitFlowScenarios.CanonicalGitFlowYaml)!;

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
        using var fixture = new EmptyRepositoryFixture("master");
        fixture.MakeATaggedCommit("1.0.0");
        fixture.AssertFullSemver("1.0.0", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-1+1", Configuration);

        fixture.MakeACommit();
        fixture.AssertFullSemver("1.0.1-1+2", Configuration);

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
    public void ForceBump_Minor_ViaCommitMessage()
    {
        using var fixture = new EmptyRepositoryFixture("master");
        fixture.MakeATaggedCommit("1.0.0");
        fixture.BranchTo("hotfix/sec-patch");
        fixture.MakeACommit("+semver: minor");
        fixture.AssertFullSemver("1.1.0-sec-patch.1+1", Configuration);
    }

    [Test]
    public void ForceBump_Patch_OnMinorBranch_DoesNotDowngradeIncrement()
    {
        using var fixture = new EmptyRepositoryFixture("master");
        fixture.MakeATaggedCommit("1.2.0");
        fixture.BranchTo("develop");
        fixture.MakeACommit("+semver: patch");
        fixture.AssertFullSemver("1.3.0-alpha.1", Configuration);
    }
}
