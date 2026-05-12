namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// A structured diagnostic emitted by the ambiguity detector.
/// Corresponds to failure contracts F-001 through F-004 in FAILURE-UX-CONTRACTS.md.
/// </summary>
/// <param name="Code">Failure contract code: "F-001", "F-002", "F-003", or "F-004".</param>
/// <param name="BranchPattern">The branch pattern that triggered the diagnostic. Null for global concerns.</param>
/// <param name="Message">Human-readable explanation — exactly as specified in FAILURE-UX-CONTRACTS.md.</param>
/// <param name="Fields">
/// Structured fields for JSON diagnostic output.
/// Each code defines specific required fields per the failure contracts.
/// </param>
public sealed record SynthesisDiagnostic(
    string Code,
    string? BranchPattern,
    string Message,
    IReadOnlyDictionary<string, object?> Fields);
