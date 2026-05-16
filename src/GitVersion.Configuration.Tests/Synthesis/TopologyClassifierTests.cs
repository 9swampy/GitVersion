using GitVersion.Configuration.Synthesis;

namespace GitVersion.Configuration.Tests.Synthesis;

[TestFixture]
public class TopologyClassifierTests
{
    private readonly TopologyClassifier _sut = new();

    // ── GitFlow identification ────────────────────────────────────────────────────

    [Test]
    public void GitFlowBranchSet_IdentifiedAsGitFlow()
        => _sut.Classify(["master", "develop", "feature/Branch", "release/1.2.3", "hotfix/Branch", "bugfix/Branch"])
               .Kind.ShouldBe(TopologyKind.GitFlow);

    [Test]
    public void MainBasedGitFlowBranches_IdentifiedAsGitFlow()
        => _sut.Classify(["main", "develop", "feature/Branch", "release/1.2.3"])
               .Kind.ShouldBe(TopologyKind.GitFlow);

    [Test]
    public void PrimsActualBranchNames_IdentifiedAsGitFlow()
        => _sut.Classify(["master", "develop", "feature/Branch", "bugfix/Branch", "release/1.2.3", "hotfix/Branch"])
               .Kind.ShouldBe(TopologyKind.GitFlow);

    [Test]
    public void GitFlowTopology_SuggestsGitFlowV1AsDefaultStartingPoint()
        => _sut.Classify(["master", "develop", "feature/Branch", "release/1.2.3"])
               .ShouldBe(CommonTopologies.GitFlow);

    [Test]
    public void GitFlowTopology_IsDistinctFromTrunkBased()
        => _sut.Classify(["master", "develop", "release/1.62.0", "feature/Branch"])
               .Kind.ShouldNotBe(TopologyKind.TrunkBased);

    [Test]
    public void BothGitFlowSignals_ResolveTopologyDeterministically()
        => _sut.Classify(["master", "develop", "release/1.62.0"])
               .Kind.ShouldNotBe(TopologyKind.Unknown);

    [Test]
    public void BothGitFlowSignals_PreventHybridClassification()
        => _sut.Classify(["master", "develop", "release/1.62.0"])
               .Kind.ShouldNotBe(TopologyKind.Hybrid);

    // ── TrunkBased identification ─────────────────────────────────────────────────

    [Test]
    public void TrunkBasedBranchSet_IdentifiedAsTrunkBased()
        => _sut.Classify(["main", "feature/Branch", "hotfix/Branch"])
               .Kind.ShouldBe(TopologyKind.TrunkBased);

    [Test]
    public void MasterBasedTrunkBranches_IdentifiedAsTrunkBased()
        => _sut.Classify(["master", "feature/Branch"])
               .Kind.ShouldBe(TopologyKind.TrunkBased);

    [Test]
    public void TrunkBasedTopology_SuggestsTrunkBasedPreview1AsDefaultStartingPoint()
        => _sut.Classify(["main", "feature/Branch", "hotfix/Branch"])
               .ShouldBe(CommonTopologies.TrunkBased);

    [Test]
    public void TrunkBasedTopology_IsDistinctFromGitFlow()
        => _sut.Classify(["main", "feature/Branch", "hotfix/Branch"])
               .Kind.ShouldNotBe(TopologyKind.GitFlow);

    [Test]
    public void TrunkBasedSignals_ResolveTopologyDeterministically()
        => _sut.Classify(["main", "feature/Branch"])
               .Kind.ShouldNotBe(TopologyKind.Unknown);

    [Test]
    public void TrunkBasedSignals_DoNotProduceHybridResult()
        => _sut.Classify(["main", "feature/Branch"])
               .Kind.ShouldNotBe(TopologyKind.Hybrid);

    // ── GitFlow signals exclude TrunkBased ───────────────────────────────────────

    [Test]
    public void DevelopBranch_IsGitFlowSpecific_ExcludesTrunkBased()
        => _sut.Classify(["main", "develop", "feature/Branch"])
               .Kind.ShouldNotBe(TopologyKind.TrunkBased);

    [Test]
    public void ReleaseBranch_IsGitFlowSpecific_ExcludesTrunkBased()
        // TrunkBased releases via tags — release branches signal GitFlow
        => _sut.Classify(["main", "release/1.0.0", "feature/Branch"])
               .Kind.ShouldNotBe(TopologyKind.TrunkBased);

    // ── Hybrid: partial signals present ──────────────────────────────────────────

    [Test]
    public void IntegrationStreamWithoutReleaseTrack_ClassifiedAsHybrid()
        => _sut.Classify(["master", "develop", "feature/Branch"])
               .Kind.ShouldBe(TopologyKind.Hybrid);

    [Test]
    public void ReleaseBranchWithoutIntegrationStream_ClassifiedAsHybrid()
        => _sut.Classify(["master", "release/1.2.3", "feature/Branch"])
               .Kind.ShouldBe(TopologyKind.Hybrid);

    [Test]
    public void AmbiguousTopology_CannotSuggestADefaultExemplar()
        => _sut.Classify(["master", "develop", "feature/Branch"])
               .ExemplarName.ShouldBeNull();

    // ── Unknown: insufficient signals ────────────────────────────────────────────

    [Test]
    public void InsufficientBranchSignals_TopologyUnresolvable()
        => _sut.Classify(["master"]).Kind.ShouldBe(TopologyKind.Unknown);

    [Test]
    public void NoBranchesProvided_TopologyUnresolvable()
        => _sut.Classify([]).Kind.ShouldBe(TopologyKind.Unknown);

    [Test]
    public void UnresolvableTopology_CannotSuggestADefaultExemplar()
        => _sut.Classify(["master"]).ExemplarName.ShouldBeNull();

    [Test]
    public void InsufficientSignals_DoNotProduceGitFlowClassification()
        => _sut.Classify(["master"]).Kind.ShouldNotBe(TopologyKind.GitFlow);

    [Test]
    public void InsufficientSignals_DoNotProduceTrunkBasedClassification()
        => _sut.Classify(["master"]).Kind.ShouldNotBe(TopologyKind.TrunkBased);

    [Test]
    public void InsufficientSignals_DoNotProduceHybridClassification()
        => _sut.Classify(["master"]).Kind.ShouldNotBe(TopologyKind.Hybrid);

    // ── Bare YAML keys (branch config keys, not user-supplied patterns) ──────────

    [Test]
    public void YamlBranchKeys_MatchGitFlowTopologyWithoutSlashPatterns()
        => _sut.Classify(["master", "develop", "release", "feature", "hotfix", "bugfix"])
               .Kind.ShouldBe(TopologyKind.GitFlow);

    [Test]
    public void YamlTrunkBasedKeys_IdentifiedAsTrunkBased()
        => _sut.Classify(["main", "feature", "hotfix"])
               .Kind.ShouldBe(TopologyKind.TrunkBased);

    // ── Non-standard branch names ─────────────────────────────────────────────────

    [Test]
    public void UnrecognisedBranchNames_CannotBeClassifiedAsGitFlow()
        => _sut.Classify(["main", "work/Feature", "fix/Bug"])
               .Kind.ShouldNotBe(TopologyKind.GitFlow);

    [Test]
    public void UnrecognisedBranchNames_CannotBeClassifiedAsTrunkBased()
        // "work/*" is not a recognised feature prefix — topology cannot be determined
        => _sut.Classify(["main", "work/Feature", "fix/Bug"])
               .Kind.ShouldNotBe(TopologyKind.TrunkBased);
}
