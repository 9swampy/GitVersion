using GitVersion.Configuration;
using GitVersion.Testing.Extensions;

namespace GitVersion.Core.Tests.IntegrationTests;

/// <summary>
/// Validates that CanonicalTrunkBasedYaml (the deliverable) produces identical
/// versioning behaviour to the builder-derived Configuration in CanonicalTrunkBasedScenarios.
/// </summary>
[TestFixture]
public class CanonicalTrunkBasedScenariosFromYaml
{
    private static readonly IGitVersionConfiguration Configuration =
        new ConfigurationSerializer().ReadConfiguration(CanonicalTrunkBasedScenarios.CanonicalTrunkBasedYaml)!;

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
    public void FeatureBranch_ProducesMinorPrerelease()
    {
        using var fixture = new EmptyRepositoryFixture("master");
        fixture.MakeATaggedCommit("1.0.0");
        fixture.BranchTo("feature/my-feature");
        fixture.AssertFullSemver("1.1.0-my-feature.0", Configuration);
        fixture.MakeACommit();
        fixture.AssertFullSemver("1.1.0-my-feature.1", Configuration);
    }

    [Test]
    public void HotfixBranch_ProducesPatchPrerelease()
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
}
