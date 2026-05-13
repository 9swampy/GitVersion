namespace GitVersion.Core.Tests.Configuration;

/// <summary>
/// Unit tests for individual semantic rules — isolated minimal configurations.
/// One reason to change: the rule under test changes.
/// </summary>
[TestFixture]
public class SemanticRuleUnitTests
{
    [Test]
    public void Sem001_Fires_WhenIsMainBranchAndIsReleaseBranchBothTrue()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              main:
                regex: '^master$|^main$'
                label: ''
                increment: Patch
                is-main-branch: true
                is-release-branch: true
            """);

        violations.ShouldContain(v => v.RuleId == "SEM-001" && v.BranchName == "main");
    }

    [Test]
    public void Sem001_Fires_WhenIsReleaseBranchRegexHasNoVersionPattern()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              master:
                regex: '^master$'
                label: ''
                increment: Patch
                is-release-branch: true
            """);

        violations.ShouldContain(v => v.RuleId == "SEM-001" && v.BranchName == "master");
    }

    [Test]
    public void Sem001_DoesNotFire_WhenReleaseBranchRegexContainsVersionPattern()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              release:
                regex: '^releases?[/-](?<BranchName>.+)'
                label: beta
                increment: Minor
                is-release-branch: true
            """);

        violations.ShouldNotContain(v => v.RuleId == "SEM-001");
    }

    [Test]
    public void Sem002_Fires_WhenMainBranchSourcesAPrereleaseLabelledBranch()
    {
        // develop carries label: alpha — a primary branch sourcing it would inherit the label
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              main:
                regex: '^main$'
                label: ''
                increment: Patch
                is-main-branch: true
                source-branches: [develop]
              develop:
                regex: '^develop$'
                label: alpha
                increment: Minor
            """);

        violations.ShouldContain(v => v.RuleId == "SEM-002" && v.BranchName == "main");
    }

    [Test]
    public void Sem002_DoesNotFire_WhenMainBranchSourcesEmptyLabelBranch()
    {
        // support branches have is-main-branch: true and source-branches: [main] — legitimate topology
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              main:
                regex: '^main$'
                label: ''
                increment: Patch
                is-main-branch: true
                source-branches: []
              support:
                regex: '^support[/-](?<BranchName>.+)'
                label: ''
                increment: Patch
                is-main-branch: true
                source-branches: [main]
            """);

        violations.ShouldNotContain(v => v.RuleId == "SEM-002");
    }

    [Test]
    public void Sem003_Fires_WhenBranchNameLabelHasNoCaptureGroup()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              hotfix:
                regex: '^hotfix/'
                label: '{BranchName}'
                increment: Patch
            """);

        violations.ShouldContain(v => v.RuleId == "SEM-003" && v.BranchName == "hotfix");
    }

    [Test]
    public void Sem003_DoesNotFire_WhenCaptureGroupPresent()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              hotfix:
                regex: '^hotfix/(?<BranchName>.+)'
                label: '{BranchName}'
                increment: Patch
            """);

        violations.ShouldNotContain(v => v.RuleId == "SEM-003");
    }

    [Test]
    public void Sem005_Fires_WhenSourceBranchesReferencesUndefinedKey()
    {
        var violations = SemanticValidator.Validate("""
            mode: ContinuousDelivery
            strategies: [TaggedCommit]
            branches:
              main:
                regex: '^main$'
                label: ''
                increment: Patch
                is-main-branch: true
                source-branches: []
              feature:
                regex: '^feature/(?<BranchName>.+)'
                label: '{BranchName}'
                increment: Inherit
                source-branches: [main, develop]
            """);

        violations.ShouldContain(v => v.RuleId == "SEM-005" && v.BranchName == "feature",
            "'develop' is referenced in source-branches but not defined");
    }
}
