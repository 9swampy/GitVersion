using GitVersion.Configuration.Synthesis;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

/// <summary>
/// Task 7: SemanticMapping translates DetectionOnlySynthesis output into explicit config fields.
/// DEC-014 invariants:
///   - Injective: one inference → one branch config, no sharing
///   - Explicit-only: only fields the mapper chose appear in output
///   - No canonical YAML inheritance: emitter receives nothing it wasn't given
/// </summary>
[TestFixture]
public class SemanticMappingTests
{
    private readonly DetectionOnlySynthesis _detection = new();
    private readonly SemanticMapper _sut = new();

    // ── Primary branch ────────────────────────────────────────────────────────────

    [Test]
    public void PrimaryBranch_MapsToMainBranchWithEmptyLabel()
    {
        // Minimal complete set — detection requires a version authority to succeed
        var detection = _detection.Detect([("master", "1.62.0"), ("develop", "1.62.0-alpha1"), ("release/1.62.0", "1.62.0-beta1")]);
        Assume.That(detection.IsSuccessful, "Precondition: detection must succeed before mapping");
        var config = _sut.Map(detection, IncrementSource.BranchName);

        var master = config.Branches.Single(b => b.BranchPattern == "master");
        master.Role.ShouldBe(BranchRole.Primary);
        master.Label.ShouldBe(string.Empty);
        master.SourceBranches.ShouldBeEmpty();
    }

    // ── Label carrier — static label ─────────────────────────────────────────────

    [Test]
    public void DevelopBranch_AlphaLabel_ContinuousDeployment_MappedExplicitly()
    {
        var detection = _detection.Detect([("master", "1.62.0"), ("develop", "1.62.0-alpha1243"), ("release/1.62.0", "1.62.0-beta1")]);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        var develop = config.Branches.Single(b => b.BranchPattern == "develop");
        develop.Label.ShouldBe("alpha");
        develop.Mode.ShouldBe(DeploymentMode.ContinuousDeployment);
        develop.Role.ShouldBe(BranchRole.LabelCarrier);
    }

    // ── Label carrier — {BranchName} ─────────────────────────────────────────────

    [Test]
    public void FeatureBranch_BranchNameLabel_MappedWithCaptureGroupInRegex()
    {
        var detection = _detection.Detect([("master", "1.62.0"), ("develop", "1.62.0-alpha1"), ("release/1.62.0", "1.62.0-beta1"), ("feature/Login", "1.62.0-Login42")]);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        var feature = config.Branches.Single(b => b.BranchPattern == "feature/Login");
        feature.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        feature.DerivedRegex.ShouldContain("(?<BranchName>");
    }

    // ── Version authority ─────────────────────────────────────────────────────────

    [Test]
    public void ReleaseBranch_MappedAsVersionAuthority_WithVersionPatternRegex()
    {
        var detection = _detection.Detect([("master", "1.62.0"), ("develop", "1.62.0-alpha1"), ("release/1.62.0", "1.62.0-beta1244")]);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        var release = config.Branches.Single(b => b.BranchPattern == "release/1.62.0");
        release.Role.ShouldBe(BranchRole.VersionAuthority);
        release.Label.ShouldBe("beta");
        release.DerivedRegex.ShouldContain(@"\d+\.\d+\.\d+");
    }

    // ── Injective: each inference produces exactly one branch config ──────────────

    [Test]
    public void PrimsNominalInputs_ProduceExactlyOneBranchConfigPerInput()
    {
        var detection = _detection.Detect(PrimsNominalInputs);
        Assume.That(detection.IsSuccessful);

        var config = _sut.Map(detection, IncrementSource.BranchName);

        config.Branches.Count.ShouldBe(detection.Inputs.Count);
    }

    [Test]
    public void PrimsNominalInputs_NoBranchConfigIsEmpty()
    {
        var detection = _detection.Detect(PrimsNominalInputs);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        config.Branches.ShouldAllBe(b => !string.IsNullOrEmpty(b.DerivedRegex),
            "Every mapped branch must have a derived regex — no empty mappings");
    }

    // ── Topology and root config ──────────────────────────────────────────────────

    [Test]
    public void GitFlowTopology_MapsToGitFlowStrategies()
    {
        var detection = _detection.Detect(PrimsNominalInputs);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        config.Topology.ShouldBe(CommonTopologies.GitFlow);
        config.Strategies.ShouldNotBeEmpty();
    }

    [Test]
    public void IncrementSource_IsPreservedInConfig()
    {
        var detection = _detection.Detect(PrimsNominalInputs);
        var config = _sut.Map(detection, IncrementSource.BranchName);

        config.IncrementSource.ShouldBe(IncrementSource.BranchName);
    }

    // ── Prims nominal inputs ──────────────────────────────────────────────────────

    private static readonly (string BranchPattern, string? VersionExample)[] PrimsNominalInputs =
    [
        ("master",          "1.62.0"),
        ("develop",         "1.62.0-alpha1243"),
        ("release/1.62.0",  "1.62.0-beta1244"),
        ("feature/Login",   "1.62.0-Login42"),
        ("bugfix/FixAuth",  "1.62.0-FixAuth3"),
        ("hotfix/SecPatch", "1.62.0-SecPatch1")
    ];
}
