namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// Topologies for which a validated exemplar configuration exists.
/// Each member pairs a <see cref="TopologyKind"/> with the workflow name of its proven
/// GitVersion configuration blueprint — derived from the canonical fixture suite.
/// </summary>
/// <remarks>
/// Unknown and Hybrid are intentionally absent: they have no exemplar because their
/// configuration cannot be deterministically selected without further user input.
/// </remarks>
public static class CommonTopologies
{
    /// <summary>GitFlow topology, paired with the <c>GitFlow/v1</c> workflow exemplar.</summary>
    public static readonly TopologyClassification GitFlow =
        new(TopologyKind.GitFlow, "GitFlow/v1");

    /// <summary>Trunk-based topology, paired with the <c>TrunkBased/preview1</c> workflow exemplar.</summary>
    public static readonly TopologyClassification TrunkBased =
        new(TopologyKind.TrunkBased, "TrunkBased/preview1");
}
