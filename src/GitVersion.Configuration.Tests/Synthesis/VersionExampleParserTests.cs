using GitVersion.Configuration.Synthesis;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

[TestFixture]
public class VersionExampleParserTests
{
    private readonly VersionExampleParser _sut = new();

    // ── No-prerelease versions (parser never assigns Primary) ───────────────────
    // Primary is a topology-derived role assigned by DetectionOnlySynthesis, not the parser.
    // The parser sees only one example pair and cannot know topology — it returns the
    // carrier/authority signal from the example, and detection promotes to Primary later.

    [Test]
    public void Master_CleanVersion_InfersLabelCarrierWithEmptyLabel()
    {
        var result = _sut.Parse("master", "1.62.0");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
        result.Label.ShouldBe(string.Empty);
    }

    [Test]
    public void Main_CleanVersion_InfersLabelCarrierWithEmptyLabel()
    {
        var result = _sut.Parse("main", "1.62.0");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
    }

    [Test]
    public void Release_CleanVersion_InfersVersionAuthority()
    {
        // Regression: a release branch reaching its final clean version must still be
        // classified as VersionAuthority (authority is a branch-pattern property, not a
        // version-format property). Previously a missing '-' segment short-circuited to
        // Primary via the early return — this test pins the corrected layering.
        var result = _sut.Parse("release/1.62.0", "1.62.0");

        result.Role.ShouldBe(BranchRole.VersionAuthority);
        result.Label.ShouldBe(string.Empty);
        result.SuggestedMode.ShouldBeNull();
    }

    [Test]
    public void Parser_NeverReturnsPrimary()
    {
        // Layering invariant: Primary is not derivable from a single example.
        // No (branchPattern, versionExample) input the parser can see should ever yield Primary.
        (string Branch, string Version)[] inputs =
        [
            ("master", "1.2.3"),
            ("main", "1.2.3"),
            ("develop", "1.2.3-alpha.1"),
            ("release/1.2.3", "1.2.3"),
            ("release/1.2.3", "1.2.3-beta.1"),
            ("feature/foo", "1.2.3-foo.1"),
            ("hotfix/x", "1.2.3-x.1"),
            ("bugfix/y", "1.2.3-y1")
        ];

        foreach (var (branch, version) in inputs)
        {
            var result = _sut.Parse(branch, version);
            result.Role.ShouldNotBe(BranchRole.Primary, $"Parser must not assign Primary for ({branch}, {version})");
        }
    }

    // ── Static label (carrier) ───────────────────────────────────────────────────

    [Test]
    public void Develop_AlphaConcatenated_InfersLabelCarrierContinuousDeployment()
    {
        var result = _sut.Parse("develop", "1.62.0-alpha1243");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
        result.Label.ShouldBe("alpha");
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDeployment);
    }

    [Test]
    public void Develop_AlphaDotSeparated_InfersLabelCarrierContinuousDelivery()
    {
        var result = _sut.Parse("develop", "1.62.0-alpha.1243");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
        result.Label.ShouldBe("alpha");
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDelivery);
    }

    [Test]
    public void Release_BetaConcatenated_InfersVersionAuthorityContinuousDeployment()
    {
        // Version authority: branch name contains the version (1.62.0 matches release/1.62.0)
        var result = _sut.Parse("release/1.62.0", "1.62.0-beta1244");

        result.Role.ShouldBe(BranchRole.VersionAuthority);
        result.Label.ShouldBe("beta");
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDeployment);
    }

    // ── {BranchName} label (carrier with variable label) ────────────────────────

    [Test]
    public void Feature_BranchNameConcatenated_InfersBranchNameLabel()
    {
        // "Login" matches the variable part of "feature/Login"
        var result = _sut.Parse("feature/Login", "1.62.0-Login1242");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDeployment);
    }

    [Test]
    public void Feature_PlaceholderPattern_InfersBranchNameLabel()
    {
        // User supplied "Branch" as template placeholder name
        var result = _sut.Parse("feature/Branch", "1.62.0-Branch1242");

        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
    }

    [Test]
    public void Hotfix_BranchNameDotSeparated_InfersBranchNameLabelContinuousDelivery()
    {
        var result = _sut.Parse("hotfix/SecPatch", "1.62.0-SecPatch.3");

        result.Role.ShouldBe(BranchRole.LabelCarrier);
        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDelivery);
    }

    [Test]
    public void Bugfix_BranchNameConcatenated_InfersBranchNameLabel()
    {
        var result = _sut.Parse("bugfix/FixAuth", "1.62.0-FixAuth42");

        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        result.SuggestedMode.ShouldBe(DeploymentMode.ContinuousDeployment);
    }

    // ── ManualDeployment (prerelease.counter+metadata) ───────────────────────────

    [Test]
    public void Feature_PlusMetadata_InfersManualDeployment()
    {
        var result = _sut.Parse("feature/Login", "1.62.0-Login.1+42");

        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        result.SuggestedMode.ShouldBe(DeploymentMode.ManualDeployment);
    }

    // ── Prims nominal cases ──────────────────────────────────────────────────────

    [Test]
    public void PrimsMasterExample_InfersLabelCarrier()
    {
        // Parser returns LabelCarrier; Detection promotes master/main to Primary.
        // See DetectionOnlySynthesisTests.PrimsNominalInputs_InfersMasterAsPrimary.
        var result = _sut.Parse("master", "1.62.0");
        result.Role.ShouldBe(BranchRole.LabelCarrier);
    }

    [Test]
    public void PrimsDevelopExample_InfersAlphaCarrier()
    {
        var result = _sut.Parse("develop", "1.62.0-alpha1243");
        result.Label.ShouldBe("alpha");
        result.Role.ShouldBe(BranchRole.LabelCarrier);
    }

    [Test]
    public void PrimsReleaseExample_InfersBetaAuthority()
    {
        var result = _sut.Parse("release/1.62.0", "1.62.0-beta1244");
        result.Label.ShouldBe("beta");
        result.Role.ShouldBe(BranchRole.VersionAuthority);
    }

    [Test]
    public void PrimsFeatureExample_InfersBranchNameCarrier()
    {
        var result = _sut.Parse("feature/Name", "1.62.0-Name1242");
        result.Label.ShouldBe(ConfigurationConstants.BranchNamePlaceholder);
        result.Role.ShouldBe(BranchRole.LabelCarrier);
    }
}
