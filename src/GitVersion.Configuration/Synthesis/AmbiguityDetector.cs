using System.Text.RegularExpressions;

namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Detects under-determined intent and conflicting signals in a set of synthesis inputs.
/// Emits structured diagnostics matching FAILURE-UX-CONTRACTS.md (F-001 through F-004).
/// </summary>
/// <remarks>
/// Pure function — no YAML emission, no Git access.
/// Synthesis MUST fail loudly rather than silently produce incorrect configuration.
///
/// Failure codes:
///   F-001  Increment authority ambiguous — nothing explains how the version number advances
///   F-002  Insufficient example signal — format unrecognized, mode cannot be inferred
///   F-003  Conflicting authority signals — primary branch claims version authority (SEM-001 surface)
///   F-004  Grammar not recognized — placeholder not in known variable vocabulary (pre-SEM-010)
///   F-005  Duplicate family examples — two or more inputs collapse to the same emission key
/// </remarks>
public sealed class AmbiguityDetector
{
    // Placeholders that are valid without SEM-010 ratification
    private static readonly IReadOnlySet<string> KnownPlaceholders =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BranchName", "Number" };

    private static readonly Regex UnknownPlaceholderPattern =
        new(@"\{(?<name>[A-Za-z][A-Za-z0-9]*)\}", RegexOptions.Compiled);

    private static readonly Regex PrimaryBranchPattern =
        new(@"^(main|master)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Detects ambiguities and conflicts in the provided inputs.
    /// Returns an empty list when intent is fully determined — synthesis may proceed.
    /// </summary>
    /// <param name="topology">The classified topology from Layer 1.</param>
    /// <param name="inputs">Parsed (pattern, example, inference) triples from Layer 2.</param>
    public IReadOnlyList<SynthesisDiagnostic> Detect(
        TopologyClassification topology,
        IEnumerable<SynthesisInput> inputs)
    {
        var inputList = inputs.ToList();
        var diagnostics = new List<SynthesisDiagnostic>();

        CheckIncrementAuthorityResolvable(topology, inputList, diagnostics);
        CheckExampleFormatRecognised(inputList, diagnostics);
        CheckPrimaryBranchDoesNotClaimVersionAuthority(inputList, diagnostics);
        CheckBranchPatternUsesKnownVariables(inputList, diagnostics);
        CheckBranchFamiliesAreUnique(inputList, diagnostics);

        return diagnostics.AsReadOnly();
    }

    private static void CheckBranchFamiliesAreUnique(
        IReadOnlyList<SynthesisInput> inputs,
        List<SynthesisDiagnostic> diagnostics)
    {
        // GitVersion's branches: map is keyed by family, not by instance.
        // Two inputs that derive the same BranchFamilyKey would emit colliding
        // YAML map entries — the emitter cannot recover and YamlEmitter has a
        // matching internal-failure guard. Reject the intake here so the user
        // sees an actionable diagnostic instead of a downstream invariant break.
        var collisions = inputs
            .GroupBy(i => BranchFamilyKey.Derive(i.BranchPattern))
            .Where(g => g.Count() > 1);

        foreach (var collision in collisions)
        {
            var family = collision.Key;
            var branches = collision.Select(i => i.BranchPattern).ToArray();

            diagnostics.Add(new SynthesisDiagnostic(
                "F-005",
                family,
                $"Multiple examples map to the same branch family '{family}'.\n\n" +
                "GitVersion configuration defines branches at the family level, not per-instance.\n" +
                "Only one example per branch family is permitted.\n\n" +
                "Provide a single representative example for this family or use explicit overrides.",
                new Dictionary<string, object?>
                {
                    ["code"] = "F-005",
                    ["family"] = family,
                    ["branches"] = branches,
                    ["reason"] = "DuplicateFamilyExamples",
                    ["action"] = "Provide one example per family"
                }));
        }
    }

    private static void CheckIncrementAuthorityResolvable(
        TopologyClassification topology,
        IReadOnlyList<SynthesisInput> inputs,
        List<SynthesisDiagnostic> diagnostics)
    {
        // TrunkBased: tags are the authority — no release branch needed
        if (topology.Kind == TopologyKind.TrunkBased)
            return;

        var hasAuthority = inputs.Any(i =>
            i.Inference?.Role == BranchRole.VersionAuthority);

        if (hasAuthority && topology.Kind != TopologyKind.Unknown)
            return;

        var developBranch = inputs.FirstOrDefault(i =>
            i.BranchPattern.StartsWith("dev", StringComparison.OrdinalIgnoreCase))
            ?.BranchPattern ?? "develop";

        diagnostics.Add(new SynthesisDiagnostic(
            "F-001",
            developBranch,
            $"Cannot determine what causes version increments for branch '{developBranch}'.\n\n" +
            "The examples provided show version changes, but do not specify whether those\n" +
            "changes occur due to:\n" +
            "  • commits on the branch,\n" +
            "  • merges from other branches, or\n" +
            "  • branch name / tag authority.\n\n" +
            "Please specify the increment source explicitly.",
            new Dictionary<string, object?>
            {
                ["code"] = "F-001",
                ["branch"] = developBranch,
                ["missing"] = "incrementAuthority",
                ["candidates"] = new[] { "Commits", "Merges", "Authority" },
                ["action"] = "Require explicit selection"
            }));
    }

    private static void CheckExampleFormatRecognised(
        IReadOnlyList<SynthesisInput> inputs,
        List<SynthesisDiagnostic> diagnostics)
    {
        foreach (var input in inputs)
        {
            if (input.Inference == null) continue;
            if (input.Inference.Role == BranchRole.Primary) continue;
            if (input.Inference.SuggestedMode.HasValue) continue;
            if (string.IsNullOrEmpty(input.Inference.Label)) continue;

            diagnostics.Add(new SynthesisDiagnostic(
                "F-002",
                input.BranchPattern,
                $"The provided example for '{input.BranchPattern}' is valid, but insufficient to infer versioning rules.\n\n" +
                "At least one additional example or an explicit override is required to determine:\n" +
                "  • label origin\n" +
                "  • increment behavior\n\n" +
                "No configuration has been generated.",
                new Dictionary<string, object?>
                {
                    ["code"] = "F-002",
                    ["branch"] = input.BranchPattern,
                    ["reason"] = "Underdetermined examples",
                    ["required"] = new[] { "Additional example or explicit override" }
                }));
        }
    }

    private static void CheckPrimaryBranchDoesNotClaimVersionAuthority(
        IReadOnlyList<SynthesisInput> inputs,
        List<SynthesisDiagnostic> diagnostics)
    {
        foreach (var input in inputs)
        {
            if (input.Inference?.Role != BranchRole.VersionAuthority) continue;

            // Only flag primary-named branches (master/main) — release/* claiming
            // version authority is legitimate and expected
            var isPrimaryNamed = PrimaryBranchPattern.IsMatch(input.BranchPattern.Split('/')[0]);
            if (!isPrimaryNamed) continue;

            diagnostics.Add(new SynthesisDiagnostic(
                "F-003",
                input.BranchPattern,
                $"Branch '{input.BranchPattern}' appears to both define the base version and apply a label.\n\n" +
                "A branch must be either:\n" +
                "  • a version authority, or\n" +
                "  • a label carrier\n" +
                "but not both.\n\n" +
                "Please revise the examples or provide an explicit override.",
                new Dictionary<string, object?>
                {
                    ["code"] = "F-003",
                    ["branch"] = input.BranchPattern,
                    ["rule"] = "SEM-001",
                    ["conflict"] = new[] { "Authority", "Carrier" }
                }));
        }
    }

    private static void CheckBranchPatternUsesKnownVariables(
        IReadOnlyList<SynthesisInput> inputs,
        List<SynthesisDiagnostic> diagnostics)
    {
        foreach (var input in inputs)
        {
            var matches = UnknownPlaceholderPattern.Matches(input.BranchPattern);
            foreach (Match match in matches)
            {
                var name = match.Groups["name"].Value;
                if (KnownPlaceholders.Contains(name)) continue;

                diagnostics.Add(new SynthesisDiagnostic(
                    "F-004",
                    input.BranchPattern,
                    $"The placeholder '{{{name}}}' is not recognized by GitVersion.\n\n" +
                    "Only known variables and supported format specifiers may be used.\n" +
                    "No configuration has been generated.",
                    new Dictionary<string, object?>
                    {
                        ["code"] = "F-004",
                        ["placeholder"] = $"{{{name}}}",
                        ["status"] = "UnknownGrammar"
                    }));
            }
        }
    }
}
