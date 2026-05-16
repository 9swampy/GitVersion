namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Coordinates detection-only synthesis (Appendix B Step 1): Layer 1 + Layer 2 → diagnostics.
/// No YAML is emitted. Synthesis stops here if any diagnostic is raised.
/// </summary>
/// <remarks>
/// Composes <see cref="TopologyClassifier"/>, <see cref="VersionExampleParser"/>,
/// and <see cref="AmbiguityDetector"/> as a single entry point.
/// Each collaborator has one reason to change; this class has one reason to change:
/// the coordination contract between detection stages.
/// </remarks>
public sealed class DetectionOnlySynthesis(
    TopologyClassifier classifier,
    VersionExampleParser parser,
    AmbiguityDetector detector)
{
    public DetectionOnlySynthesis()
        : this(new TopologyClassifier(), new VersionExampleParser(), new AmbiguityDetector()) { }

    /// <summary>
    /// Runs detection on a set of user-supplied (branchPattern, versionExample) pairs.
    /// </summary>
    /// <param name="userInputs">
    /// Pairs of branch name patterns and optional version examples.
    /// e.g. ("develop", "1.62.0-alpha1243"), ("master", "1.62.0"), ("feature/Login", null)
    /// </param>
    /// <returns>
    /// A <see cref="SynthesisDetectionResult"/> with topology, inferences, and any diagnostics.
    /// <see cref="SynthesisDetectionResult.IsSuccessful"/> is true only when zero diagnostics were raised.
    /// </returns>
    public SynthesisDetectionResult Detect(
        IEnumerable<(string BranchPattern, string? VersionExample)> userInputs)
    {
        var pairs = userInputs.ToList();

        var topology = classifier.Classify(pairs.Select(p => p.BranchPattern));

        var inputs = pairs.Select(p => new SynthesisInput(
            p.BranchPattern,
            p.VersionExample,
            BuildInference(p.BranchPattern, p.VersionExample)))
            .ToList();

        var diagnostics = detector.Detect(topology, inputs);

        return new SynthesisDetectionResult(topology, inputs, diagnostics);
    }

    private VersionExampleInference? BuildInference(string branchPattern, string? versionExample)
    {
        if (versionExample is null)
            return null;

        var parsed = parser.Parse(branchPattern, versionExample);

        // Layering: Primary is a topology-level role, not a parse-time signal.
        // The parser returns LabelCarrier/VersionAuthority; detection promotes
        // master/main to Primary using the single source of truth on TopologyClassifier.
        return TopologyClassifier.IsPrimary(branchPattern)
            ? parsed with { Role = BranchRole.Primary }
            : parsed;
    }
}
