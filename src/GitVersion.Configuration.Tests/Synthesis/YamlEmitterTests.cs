using GitVersion.Configuration.Synthesis;
using GitVersion.Configuration.Validation;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

/// <summary>
/// Task 8: YamlEmitter serializes SynthesisConfig to a minimal GitVersion.yml string.
/// DEC-014 constraints:
///   - Only fields explicitly set by SemanticMapper appear in output
///   - Structural scaffolding (mode, strategies, field ordering) is format, not semantics
///   - Output must be parseable by ConfigurationSerializer (round-trip)
///   - Output must pass ConfigurationSemanticValidator (no errors)
/// </summary>
[TestFixture]
public class YamlEmitterTests
{
    private readonly DetectionOnlySynthesis _detection = new();
    private readonly SemanticMapper _mapper = new();
    private readonly YamlEmitter _sut = new();
    private readonly ConfigurationSerializer _serializer = new();
    private readonly ConfigurationSemanticValidator _validator = new();

    private SynthesisConfig MapPrimsNominal() =>
        _mapper.Map(
            _detection.Detect(PrimsNominalInputs),
            IncrementSource.BranchName);

    // ── Round-trip: emitted YAML must be parseable ────────────────────────────────

    [Test]
    public void EmittedYaml_IsParseableByConfigurationSerializer()
    {
        var yaml = _sut.Emit(MapPrimsNominal());

        var config = _serializer.ReadConfiguration(yaml);

        config.ShouldNotBeNull("Emitted YAML must be parseable by ConfigurationSerializer");
    }

    [Test]
    public void EmittedYaml_ContainsAllMappedBranches()
    {
        var synthConfig = MapPrimsNominal();
        var yaml = _sut.Emit(synthConfig);
        var config = _serializer.ReadConfiguration(yaml)!;

        config.Branches.Count.ShouldBe(synthConfig.Branches.Count,
            "Every mapped branch must appear in the emitted YAML");
    }

    // ── Primary branch ────────────────────────────────────────────────────────────

    [Test]
    public void PrimaryBranch_EmittedWithIsMainBranchTrue_EmptySourceBranches()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        var master = config.Branches.Values.Single(b => b.IsMainBranch == true);
        master.Label.ShouldBe(string.Empty);
        master.SourceBranches.ShouldBeEmpty();
    }

    // ── Version authority ─────────────────────────────────────────────────────────

    [Test]
    public void VersionAuthority_EmittedWithIsReleaseBranchTrue()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        config.Branches.Values.ShouldContain(b => b.IsReleaseBranch == true,
            "Release branch must be emitted as a version authority");
    }

    // ── Label carrier ─────────────────────────────────────────────────────────────

    [Test]
    public void DevelopBranch_EmittedWithAlphaLabel()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        config.Branches.Values.ShouldContain(b => b.Label == "alpha",
            "Develop branch must emit its alpha label");
    }

    [Test]
    public void FeatureBranch_EmittedWithBranchNamePlaceholder()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        config.Branches.Values.ShouldContain(
            b => b.Label == ConfigurationConstants.BranchNamePlaceholder,
            "Feature branch must emit {BranchName} label");
    }

    // ── Strategies ────────────────────────────────────────────────────────────────

    [Test]
    public void GitFlowTopology_EmitsNonEmptyStrategiesList()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        config.VersionStrategy.ShouldNotBe(VersionStrategies.None,
            "GitFlow synthesis must emit an explicit strategies block (SEM-006)");
    }

    // ── Validator gate: emitted YAML must produce no errors ───────────────────────

    [Test]
    public void EmittedYaml_PassesSemanticValidator()
    {
        var yaml = _sut.Emit(MapPrimsNominal());
        var config = _serializer.ReadConfiguration(yaml)!;

        var violations = _validator.Validate(config);
        var errors = violations.Where(v => v.Severity == SemanticViolationSeverity.Error).ToList();

        errors.ShouldBeEmpty(
            $"Synthesis output must not produce validator errors. Errors: {string.Join(", ", errors.Select(e => $"{e.RuleId}[{e.BranchName}]"))}");
    }

    // ── Emission-key injectivity (DEC-018) ───────────────────────────────────────

    [Test]
    public void DuplicateBranchFamilyKeys_ThrowsInternalFailure()
    {
        // AmbiguityDetector emits F-005 for duplicate-family intake — but if a
        // caller bypasses detection (or a future refactor regresses it), the
        // emitter must not silently produce invalid YAML / last-write-wins
        // output. The contract is: synthesis aborts with an internal failure.
        var config = new SynthesisConfig(
            new TopologyClassification(TopologyKind.GitFlow, "GitFlow/v1"),
            new[]
            {
                new SynthesisBranchConfig(
                    "feature/Login",
                    "^feature/(?<BranchName>.+)",
                    BranchRole.LabelCarrier,
                    ConfigurationConstants.BranchNamePlaceholder,
                    DeploymentMode.ContinuousDeployment,
                    Array.Empty<string>()),
                new SynthesisBranchConfig(
                    "feature/Search",
                    "^feature/(?<BranchName>.+)",
                    BranchRole.LabelCarrier,
                    ConfigurationConstants.BranchNamePlaceholder,
                    DeploymentMode.ContinuousDeployment,
                    Array.Empty<string>())
            },
            IncrementSource.BranchName,
            new[] { "Fallback" });

        var exception = Should.Throw<InvalidOperationException>(() => _sut.Emit(config));

        exception.Message.ShouldContain("synthesis invariants",
            customMessage: "Guard must frame the failure as an internal invariant break, not a user input error");
        exception.Message.ShouldContain("feature");
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
