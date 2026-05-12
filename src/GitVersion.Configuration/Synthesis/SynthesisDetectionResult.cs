namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// The result of detection-only synthesis (Appendix B Step 1).
/// Contains topology, parsed inputs, and any diagnostics — never YAML.
/// </summary>
/// <param name="Topology">The classified topology from Layer 1 branch patterns.</param>
/// <param name="Inputs">Each user input with its parsed inference. Inference is null when no example was supplied.</param>
/// <param name="Diagnostics">
/// Structured diagnostics from ambiguity detection.
/// Empty when intent is fully determined and synthesis may proceed to YAML emission.
/// Non-empty means synthesis must stop and present explanations to the user.
/// </param>
public sealed record SynthesisDetectionResult(
    TopologyClassification Topology,
    IReadOnlyList<SynthesisInput> Inputs,
    IReadOnlyList<SynthesisDiagnostic> Diagnostics)
{
    /// <summary>
    /// True when no diagnostics were raised — intent is fully determined.
    /// False means at least one ambiguity or conflict was detected; the user must resolve it.
    /// </summary>
    public bool IsSuccessful => Diagnostics.Count == 0;
}
