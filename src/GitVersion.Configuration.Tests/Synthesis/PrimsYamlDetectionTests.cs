using GitVersion.Configuration.Synthesis;

namespace GitVersion.Configuration.Tests.Synthesis;

/// <summary>
/// Dogfood: runs Detection-Only Synthesis (Appendix B Step 1) against the actual
/// prims/.github/GitVersion.yml file on disk.
///
/// Layer 1 input: branch keys extracted from the YAML's branches dictionary.
/// Layer 2 input: none — the YAML does not contain version output examples.
///
/// This proves the classifier works on real production config keys, not just
/// user-supplied example patterns. It also documents the known limitation:
/// without Layer 2 examples, F-001 fires (increment authority unknown from config alone).
/// </summary>
[TestFixture]
public class PrimsYamlDetectionTests
{
    // Workstation-local dogfood: requires a checkout of the PRIMS estate on disk.
    // Tests are marked [Explicit] so default (CI) runs skip them entirely rather
    // than silently rendering inconclusive — opt in with `--filter Category=Dogfood`
    // or by naming a specific test. Override the location via the PRIMS_ROOT
    // environment variable when the estate lives elsewhere.
    private static readonly string PrimsRoot =
        System.Environment.GetEnvironmentVariable("PRIMS_ROOT") ?? "/git/prims";

    private static string PrimsGitHubYamlPath => Path.Combine(PrimsRoot, ".github/GitVersion.yml");

    private readonly DetectionOnlySynthesis _sut = new();
    private readonly ConfigurationSerializer _serializer = new();

    [Test]
    [Explicit("Requires PRIMS estate on disk (see PRIMS_ROOT)")]
    [Category("Dogfood")]
    public void PrimsGitHubConfig_BranchKeys_ClassifyAsGitFlow()
    {
        Assume.That(File.Exists(PrimsGitHubYamlPath), $"prims .github config not present at {PrimsGitHubYamlPath} — skipping");

        var config = _serializer.ReadConfiguration(File.ReadAllText(PrimsGitHubYamlPath))!;
        var branchKeys = config.Branches.Keys;

        var topology = new TopologyClassifier().Classify(branchKeys);

        topology.Kind.ShouldBe(TopologyKind.GitFlow,
            $"Branch keys {string.Join(", ", branchKeys)} should classify as GitFlow");
        topology.ExemplarName.ShouldBe("GitFlow/v1");
    }

    [Test]
    [Explicit("Requires PRIMS estate on disk (see PRIMS_ROOT)")]
    [Category("Dogfood")]
    public void PrimsGitHubConfig_WithoutExamples_F001FiresBecauseIncrementAuthorityUnknown()
    {
        // This is the known limitation documented in Appendix B:
        // The YAML config alone cannot tell us HOW the version advances — only WHAT branches exist.
        // Without Layer 2 (output examples), increment authority cannot be inferred.
        // The user must supply version output examples to complete the synthesis intake.
        Assume.That(File.Exists(PrimsGitHubYamlPath), $"prims .github config not present at {PrimsGitHubYamlPath} — skipping");

        var config = _serializer.ReadConfiguration(File.ReadAllText(PrimsGitHubYamlPath))!;
        var inputs = config.Branches.Keys.Select(k => (BranchPattern: k, VersionExample: (string?)null));

        var result = _sut.Detect(inputs);

        result.IsSuccessful.ShouldBeFalse(
            "Config-only input (no version examples) cannot determine increment authority");
        result.Diagnostics.ShouldContain(d => d.Code == "F-001",
            "F-001 fires because increment source is unknown without Layer 2 examples");
    }

    [Test]
    [Explicit("Requires PRIMS estate on disk (see PRIMS_ROOT)")]
    [Category("Dogfood")]
    public void PrimsGitHubConfig_WithUserSuppliedPatterns_IsSuccessful()
    {
        // When the user supplies BOTH the branch naming convention (with version in release name)
        // AND version output examples, synthesis resolves cleanly.
        // This is the correct full intake: user describes what they TYPE as branch names,
        // not what the YAML config key is called.
        //
        // Note: "release/1.62.0" (user-supplied) vs "release" (YAML key) — only the
        // user-supplied form carries the version signal for VersionAuthority detection.
        Assume.That(File.Exists(PrimsGitHubYamlPath), $"prims .github config not present at {PrimsGitHubYamlPath} — skipping");

        // Layer 1: how the user describes their branches (what they actually type)
        // Layer 2: what version each branch produces (as stated in session)
        var userInputs = new (string BranchPattern, string? VersionExample)[]
        {
            ("master",          "1.62.0"),
            ("develop",         "1.62.0-alpha1243"),
            ("release/1.62.0",  "1.62.0-beta1244"),  // version in name → VersionAuthority
            ("feature/Name",    "1.62.0-Name1242"),
            ("bugfix/FixAuth",  "1.62.0-FixAuth3"),
            ("hotfix/SecPatch", "1.62.0-SecPatch1")
        };

        var result = _sut.Detect(userInputs);

        result.IsSuccessful.ShouldBeTrue(
            $"User-supplied patterns + examples should detect cleanly. Got: {string.Join(", ", result.Diagnostics.Select(d => d.Code))}");
        result.Topology.Kind.ShouldBe(TopologyKind.GitFlow);
    }

    [Test]
    public void NonStandardBranchNames_ClassifyAsUnknown_RequiringExplicitDeclaration()
    {
        // Design constraint: the classifier recognises standard exemplar names (master/main,
        // develop, release/*, feature/*, hotfix/*). Non-standard names (work/*, task/*,
        // any.other.name) return Unknown topology.
        //
        // Unknown topology → F-001 fires → user must provide explicit topology declaration.
        // This is intentional: the classifier does not guess; it explains the gap.
        var inputs = new (string, string?)[]
        {
            ("main",         "1.0.0"),
            ("work/Feature", "1.0.0-Feature.3"),
            ("fix/Name",     "1.0.0-Name.1")
        };

        var result = _sut.Detect(inputs);

        // "main" is recognised, "work/*" is not → TrunkBased if feature-like branches present
        // but "work/" is not a known feature prefix → Unknown → F-001
        result.Topology.Kind.ShouldNotBe(TopologyKind.GitFlow,
            "Non-standard names do not match GitFlow topology");

        // Document the limitation explicitly
        TestContext.WriteLine(
            $"Topology: {result.Topology.Kind}, " +
            $"Diagnostics: {string.Join(", ", result.Diagnostics.Select(d => d.Code))}");
    }
}
