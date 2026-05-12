using GitVersion.Configuration.Synthesis;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

/// <summary>
/// Task 5: One worked GitFlow synthesis trace — detection only, no YAML emission.
/// Exercises the full chain: user inputs → topology → example alignment → diagnostic output.
/// Proves Appendix B Step 1 (B.8) is complete.
///
/// Worked example: prims nominal inputs (the problem that motivated this work item).
/// </summary>
[TestFixture]
public class DetectionOnlySynthesisTests
{
    private readonly DetectionOnlySynthesis _sut = new();

    // ── Happy path: prims nominal inputs ─────────────────────────────────────────

    [Test]
    public void PrimsNominalInputs_DetectsGitFlowTopology()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        result.Topology.Kind.ShouldBe(TopologyKind.GitFlow);
        result.Topology.ExemplarName.ShouldBe("GitFlow/v1");
    }

    [Test]
    public void PrimsNominalInputs_ProducesNoDiagnostics()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        result.IsSuccessful.ShouldBeTrue(
            $"Prims nominal inputs should produce no diagnostics. Got: {string.Join(", ", result.Diagnostics.Select(d => d.Code))}");
        result.Diagnostics.ShouldBeEmpty();
    }

    [Test]
    public void PrimsNominalInputs_InfersMasterAsPrimary()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        var master = result.Inputs.Single(i => i.BranchPattern == "master");
        master.Inference!.Role.ShouldBe(BranchRole.Primary);
        master.Inference.Label.ShouldBe(string.Empty);
    }

    [Test]
    public void PrimsNominalInputs_InfersDevelopAsAlphaCarrier()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        var develop = result.Inputs.Single(i => i.BranchPattern == "develop");
        develop.Inference!.Role.ShouldBe(BranchRole.LabelCarrier);
        develop.Inference.Label.ShouldBe("alpha");
        develop.Inference.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDeployment);
    }

    [Test]
    public void PrimsNominalInputs_InfersReleaseAsBetaAuthority()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        var release = result.Inputs.Single(i => i.BranchPattern == "release/1.62.0");
        release.Inference!.Role.ShouldBe(BranchRole.VersionAuthority);
        release.Inference.Label.ShouldBe("beta");
    }

    [Test]
    public void PrimsNominalInputs_InfersFeatureAsBranchNameCarrier()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        var feature = result.Inputs.Single(i => i.BranchPattern == "feature/Login");
        feature.Inference!.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        feature.Inference.Role.ShouldBe(BranchRole.LabelCarrier);
    }

    // ── Failure path: ambiguous inputs ───────────────────────────────────────────

    [Test]
    public void GitFlowWithoutRelease_IsNotSuccessful()
    {
        var inputs = new (string, string?)[]
        {
            ("master",  "1.62.0"),
            ("develop", "1.62.0-alpha1243"),
            ("feature/Login", "1.62.0-Login42")
        };

        var result = _sut.Detect(inputs);

        result.IsSuccessful.ShouldBeFalse();
        result.Diagnostics.ShouldContain(d => d.Code == "F-001");
    }

    [Test]
    public void UnknownTopology_IsNotSuccessful()
    {
        var inputs = new (string, string?)[]
        {
            ("exotic-branch", "1.0.0-something.1")
        };

        var result = _sut.Detect(inputs);

        result.IsSuccessful.ShouldBeFalse();
    }

    [Test]
    public void TrunkBasedNominalInputs_IsSuccessful()
    {
        var inputs = new (string, string?)[]
        {
            ("main",           "1.62.0"),
            ("feature/Login",  "1.62.0-Login.3"),
            ("hotfix/SecPatch","1.62.0-SecPatch.1")
        };

        var result = _sut.Detect(inputs);

        result.IsSuccessful.ShouldBeTrue();
        result.Topology.Kind.ShouldBe(TopologyKind.TrunkBased);
    }

    // ── Trace completeness: all inputs have inferences ───────────────────────────

    [Test]
    public void PrimsNominalInputs_AllInputsHaveInferences()
    {
        var result = _sut.Detect(PrimsNominalInputs);

        result.Inputs.ShouldAllBe(i => i.Inference != null,
            "Every input with an example must produce an inference");
    }

    // ── Worked trace data ────────────────────────────────────────────────────────

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
