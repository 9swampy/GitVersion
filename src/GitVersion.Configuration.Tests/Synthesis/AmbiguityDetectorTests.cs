using GitVersion.Configuration.Synthesis;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

[TestFixture]
public class AmbiguityDetectorTests
{
    private readonly AmbiguityDetector _sut = new();
    private readonly VersionExampleParser _parser = new();

    private SynthesisInput Input(string pattern, string? example = null) =>
        new(pattern, example, example is null ? null : _parser.Parse(pattern, example));

    // ── F-001: Increment Authority Ambiguity ─────────────────────────────────────

    [Test]
    public void GitFlow_WithReleaseBranch_NoF001()
    {
        // Release branch is a VersionAuthority — increment source is clear
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",         "1.62.0"),
            Input("develop",        "1.62.0-alpha1243"),
            Input("release/1.62.0", "1.62.0-beta1244"),
            Input("feature/Login",  "1.62.0-Login42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldNotContain(d => d.Code == "F-001");
    }

    [Test]
    public void TrunkBased_WithoutReleaseBranch_NoF001()
    {
        // TrunkBased uses tags — increment source is known by convention
        var topology = new TopologyClassification(TopologyKind.TrunkBased, "TrunkBased/preview1");
        var inputs = new[]
        {
            Input("master",        "1.62.0"),
            Input("feature/Login", "1.62.0-Login42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldNotContain(d => d.Code == "F-001");
    }

    [Test]
    public void GitFlow_WithoutReleaseBranch_EmitsF001()
    {
        // No VersionAuthority branch — cannot determine what advances the version number
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",        "1.62.0"),
            Input("develop",       "1.62.0-alpha1243"),
            Input("feature/Login", "1.62.0-Login42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        var f001 = diagnostics.Where(d => d.Code == "F-001").ToList();
        f001.ShouldNotBeEmpty();
        f001[0].Fields["missing"].ShouldBe("incrementAuthority");
    }

    [Test]
    public void UnknownTopology_WithExamples_EmitsF001()
    {
        var topology = new TopologyClassification(TopologyKind.Unknown, null);
        var inputs = new[] { Input("master", "1.62.0"), Input("develop", "1.62.0-alpha1") };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldContain(d => d.Code == "F-001");
    }

    // ── F-002: Insufficient Example Signal ───────────────────────────────────────

    [Test]
    public void UnrecognisedFormat_EmitsF002()
    {
        // Format "1.62.0-alpha-unusual" doesn't match any known separator pattern
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("develop", "1.62.0-alpha-unusual-format")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        var f002 = diagnostics.Where(d => d.Code == "F-002").ToList();
        f002.ShouldNotBeEmpty();
        f002[0].Fields["branch"].ShouldBe("develop");
    }

    // ── F-003: Conflicting Authority Signals (SEM-001 surface) ───────────────────

    [Test]
    public void PrimaryBranchWithVersionInName_EmitsF003()
    {
        // master/1.62.0 claims VersionAuthority — primary branch cannot also be release branch
        var topology = new TopologyClassification(TopologyKind.Unknown, null);
        var inputs = new[]
        {
            Input("master/1.62.0", "1.62.0-beta1")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldContain(d => d.Code == "F-003");
    }

    [Test]
    public void ReleaseBranch_WithBetaLabel_NoF003()
    {
        // release/1.62.0 is legitimately both VersionAuthority and has a label
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("release/1.62.0", "1.62.0-beta1244")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldNotContain(d => d.Code == "F-003");
    }

    // ── F-004: Grammar Not Recognized ────────────────────────────────────────────

    [Test]
    public void UnknownPlaceholder_EmitsF004()
    {
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("feature/{VersionCoreX}", "1.62.0-VersionCoreX1")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        var f004 = diagnostics.Where(d => d.Code == "F-004").ToList();
        f004.ShouldNotBeEmpty();
        f004[0].Fields["status"].ShouldBe("UnknownGrammar");
    }

    [Test]
    public void KnownPlaceholders_NoF004()
    {
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("feature/Branch",  "1.62.0-Branch42"),
            Input("release/1.62.0",  "1.62.0-beta1244")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldNotContain(d => d.Code == "F-004");
    }

    // ── F-005: Duplicate Family Examples ─────────────────────────────────────────

    [Test]
    public void TwoExamplesOfSameFeatureFamily_EmitsF005()
    {
        // Both inputs derive the same emission key ("feature") via BranchFamilyKey.
        // GitVersion's branches: map allows only one entry per family, so the
        // detector must reject this at intake before the mapper produces colliding
        // SynthesisBranchConfig records.
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",         "1.62.0"),
            Input("develop",        "1.62.0-alpha1243"),
            Input("release/1.62.0", "1.62.0-beta1244"),
            Input("feature/Login",  "1.62.0-Login42"),
            Input("feature/Search", "1.62.0-Search42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        var f005 = diagnostics.SingleOrDefault(d => d.Code == "F-005");
        f005.ShouldNotBeNull("Duplicate family intake must surface as F-005");
        f005!.Fields["family"].ShouldBe("feature");
        ((string[])f005.Fields["branches"]!).ShouldBe(new[] { "feature/Login", "feature/Search" });
        f005.Fields["reason"].ShouldBe("DuplicateFamilyExamples");
    }

    [Test]
    public void ThreeExamplesOfSameFeatureFamily_EmitsSingleF005()
    {
        // One diagnostic per family — not one per duplicate — so the message
        // can list the full colliding set and the user fixes the intake in one step.
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",         "1.62.0"),
            Input("develop",        "1.62.0-alpha1243"),
            Input("release/1.62.0", "1.62.0-beta1244"),
            Input("feature/A",      "1.62.0-A42"),
            Input("feature/B",      "1.62.0-B42"),
            Input("feature/C",      "1.62.0-C42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.Count(d => d.Code == "F-005").ShouldBe(1);
    }

    [Test]
    public void DistinctFamilies_NoF005()
    {
        // feature/Login and release/1.62.0 derive distinct family keys
        // ("feature" and "release") — no collision, no F-005.
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",         "1.62.0"),
            Input("develop",        "1.62.0-alpha1243"),
            Input("release/1.62.0", "1.62.0-beta1244"),
            Input("feature/Login",  "1.62.0-Login42")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldNotContain(d => d.Code == "F-005");
    }

    // ── Clean set — prims nominal inputs ─────────────────────────────────────────

    [Test]
    public void PrimsNominalInputs_ProduceNoDiagnostics()
    {
        var topology = new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1");
        var inputs = new[]
        {
            Input("master",         "1.62.0"),
            Input("develop",        "1.62.0-alpha1243"),
            Input("release/1.62.0", "1.62.0-beta1244"),
            Input("feature/Login",  "1.62.0-Login42"),
            Input("bugfix/FixAuth", "1.62.0-FixAuth3"),
            Input("hotfix/SecPatch","1.62.0-SecPatch1")
        };

        var diagnostics = _sut.Detect(topology, inputs);

        diagnostics.ShouldBeEmpty($"Nominal prims inputs should produce no diagnostics. Got: {string.Join(", ", diagnostics.Select(d => d.Code))}");
    }
}
