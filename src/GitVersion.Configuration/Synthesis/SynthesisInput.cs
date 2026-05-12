namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// A single (branchPattern, versionExample) pair with its parsed inference.
/// </summary>
/// <param name="BranchPattern">Branch name pattern as supplied by the user, e.g. "develop", "feature/Login".</param>
/// <param name="VersionExample">User-supplied example version string, e.g. "1.62.0-alpha1243". Null if not provided.</param>
/// <param name="Inference">Parsed inference from the example. Null when no example was supplied.</param>
public sealed record SynthesisInput(
    string BranchPattern,
    string? VersionExample,
    VersionExampleInference? Inference);
