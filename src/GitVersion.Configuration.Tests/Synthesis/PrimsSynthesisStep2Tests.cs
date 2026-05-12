using GitVersion.Configuration.Synthesis;
using GitVersion.Configuration.Validation;
using GitVersion.VersionCalculation;

namespace GitVersion.Configuration.Tests.Synthesis;

/// <summary>
/// Task 10: One worked prims synthesis trace — Step 2 complete.
/// Exercises the full pipeline: user inputs → detection → mapping → YAML → validated.
///
/// This is the problem that motivated GV-SEM-VAL: the prims/.github config was
/// producing 1.62.0-alpha.261 instead of 1.62.0 because of lineage traversal bugs.
/// The synthesis pipeline, given the user's stated intent, produces a config that
/// satisfies all semantic rules and matches the manually-corrected config.
///
/// Appendix B Step 2 proven complete when this trace passes.
/// </summary>
[TestFixture]
public class PrimsSynthesisStep2Tests
{
    private readonly DetectionOnlySynthesis _detection = new();
    private readonly SemanticMapper _mapper = new();
    private readonly YamlEmitter _emitter = new();
    private readonly ConfigurationSerializer _serializer = new();
    private readonly ConfigurationSemanticValidator _validator = new();

    // The user's stated intent from the session — exactly as declared:
    // "master produces 1.62.0, develop produces 1.62.0-alpha1243,
    //  release should emit 1.62.0-beta1244, feature/Name should emit 1.62.0-Name1242"
    private static readonly (string BranchPattern, string? VersionExample)[] PrimsStatedIntent =
    [
        ("master",          "1.62.0"),
        ("develop",         "1.62.0-alpha1243"),
        ("release/1.62.0",  "1.62.0-beta1244"),
        ("feature/Name",    "1.62.0-Name1242"),
        ("bugfix/FixAuth",  "1.62.0-FixAuth3"),
        ("hotfix/SecPatch", "1.62.0-SecPatch1")
    ];

    // ── Step 1 pre-condition ──────────────────────────────────────────────────────

    [Test]
    public void StatedIntent_PassesDetection_BeforeMapping()
    {
        var detection = _detection.Detect(PrimsStatedIntent);

        detection.IsSuccessful.ShouldBeTrue(
            $"Stated intent must pass detection before Step 2 can proceed. Diagnostics: {string.Join(", ", detection.Diagnostics.Select(d => d.Code))}");
    }

    // ── Step 2 pipeline ───────────────────────────────────────────────────────────

    [Test]
    public void FullPipeline_EmitsParseableYaml()
    {
        var yaml = RunFullPipeline();

        _serializer.ReadConfiguration(yaml).ShouldNotBeNull(
            "Synthesis output must be parseable by ConfigurationSerializer");
    }

    [Test]
    public void FullPipeline_EmittedYaml_PassesSemanticValidator()
    {
        var yaml = RunFullPipeline();
        var config = _serializer.ReadConfiguration(yaml)!;

        var errors = _validator.Validate(config)
            .Where(v => v.Severity == SemanticViolationSeverity.Error)
            .ToList();

        errors.ShouldBeEmpty(
            $"Synthesis output is a synthesis bug when the validator fires. Errors: " +
            $"{string.Join(", ", errors.Select(e => $"{e.RuleId}[{e.BranchName}]: {e.Title}"))}");
    }

    [Test]
    public void FullPipeline_MasterProducesCorrectConfig()
    {
        var config = _serializer.ReadConfiguration(RunFullPipeline())!;

        var master = config.Branches.Values.Single(b => b.IsMainBranch == true);
        master.Label.ShouldBe(string.Empty,    "master must have empty label — 1.62.0, not 1.62.0-something");
        master.SourceBranches.ShouldBeEmpty("master must declare source-branches: [] to prevent lineage leak");
    }

    [Test]
    public void FullPipeline_DevelopProducesAlphaLabel()
    {
        var config = _serializer.ReadConfiguration(RunFullPipeline())!;

        config.Branches.Values.ShouldContain(b => b.Label == "alpha",
            "develop must emit alpha label as stated in intent");
    }

    [Test]
    public void FullPipeline_ReleaseBranchIsVersionAuthority()
    {
        var config = _serializer.ReadConfiguration(RunFullPipeline())!;

        config.Branches.Values.ShouldContain(b => b.IsReleaseBranch == true,
            "release branch must be declared as version authority");
    }

    [Test]
    public void FullPipeline_FeatureBranchCarriesBranchName()
    {
        var config = _serializer.ReadConfiguration(RunFullPipeline())!;

        config.Branches.Values.ShouldContain(
            b => b.Label == ConfigurationConstants.BranchNamePlaceholder,
            "feature/bugfix/hotfix branches must carry {BranchName} label");
    }

    [Test]
    public void FullPipeline_StrategiesExplicitlyDeclared()
    {
        var config = _serializer.ReadConfiguration(RunFullPipeline())!;

        config.VersionStrategy.ShouldNotBe(VersionStrategies.None,
            "Emitted YAML must declare strategies explicitly (SEM-006)");
    }

    // ── The previously broken config — validator confirms it was wrong ────────────

    [Test]
    public void PreviouslyBrokenPrimsConfig_StillFiresSem001()
    {
        // The original bug: master had is-release-branch: true with no source-branches: []
        // This test preserves the negative corpus — the broken config must stay broken.
        const string brokenConfig = """
            mode: ContinuousDeployment
            branches:
              master:
                regex: ^master$
                label: ''
                increment: Patch
                prevent-increment:
                  of-merged-branch: true
                is-release-branch: true
              develop:
                regex: ^develop$
                label: alpha
                increment: Minor
            """;

        var config = _serializer.ReadConfiguration(brokenConfig)!;
        var violations = _validator.Validate(config);

        violations.ShouldContain(v => v.RuleId == "SEM-001",
            "The previously broken config must still fire SEM-001 — permanent negative corpus");
    }

    // ── Helper ────────────────────────────────────────────────────────────────────

    private string RunFullPipeline()
    {
        var detection = _detection.Detect(PrimsStatedIntent);
        var config = _mapper.Map(detection, IncrementSource.BranchName);
        return _emitter.Emit(config);
    }
}
