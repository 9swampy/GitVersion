namespace GitVersion.Configuration.Synthesis;

public enum TopologyKind { GitFlow, TrunkBased, Hybrid, Unknown }

/// <summary>
/// The result of classifying a set of branch name patterns against known topology models.
/// </summary>
/// <param name="Kind">The inferred topology kind.</param>
/// <param name="ExemplarName">
/// The GitVersion workflow name that best represents this topology,
/// e.g. "GitFlow/v1" or "TrunkBased/preview1". Null when topology is Hybrid or Unknown.
/// </param>
public sealed record TopologyClassification(TopologyKind Kind, string? ExemplarName);
