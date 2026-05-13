using GitVersion.Core.Tests.IntegrationTests;

namespace GitVersion.Core.Tests.Configuration;

/// <summary>
/// Positive corpus: canonical configs must produce zero violations.
/// Contract: ADR-001 (GitFlow) and TrunkBased (preview1) satisfy all semantic rules.
/// </summary>
[TestFixture]
public class CanonicalConfigurationSemanticTests
{
    [Test]
    public void CanonicalGitFlow_ProducesNoViolations() =>
        SemanticValidator.Validate(CanonicalGitFlowScenarios.CanonicalGitFlowYaml).ShouldBeEmpty();

    [Test]
    public void CanonicalTrunkBased_ProducesNoViolations() =>
        SemanticValidator.Validate(CanonicalTrunkBasedScenarios.CanonicalTrunkBasedYaml).ShouldBeEmpty();
}
