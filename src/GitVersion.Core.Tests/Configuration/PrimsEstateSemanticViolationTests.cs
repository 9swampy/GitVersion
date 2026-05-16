using GitVersion.Configuration.Validation;

namespace GitVersion.Core.Tests.Configuration;

/// <summary>
/// Negative corpus: PRIMS estate configs are permanent red test cases.
/// Each test asserts WHICH invariant is violated, not just that a violation exists.
/// Per GV-SEM-VAL Refinement #2: invariant-specific assertions, not "fails validation."
///
/// These configs are assets — never clean up without preserving their violation signatures.
/// Source: /git/prims/foundation/GitVersion.yml and /git/prims/strata/GitVersion.yml
/// </summary>
[TestFixture]
public class PrimsEstateSemanticViolationTests
{
    private static IReadOnlySet<string> RuleIds(string yaml) =>
        SemanticValidator.Validate(yaml).Select(v => v.RuleId).ToHashSet();

    [TestCase(PrimsFoundationYaml, TestName = "Foundation")]
    [TestCase(PrimsStrataYaml,     TestName = "Strata")]
    public void Sem001_FiresOnMasterAndRelease_BothHaveUnfulfillableAuthorityRegex(string yaml)
    {
        // master: is-release-branch:true, regex '^master$' — no version pattern, no capture group
        // release: is-release-branch:true, regex '^release/' — no capture group
        var violations = SemanticValidator.Validate(yaml).Where(v => v.RuleId == "SEM-001").ToList();

        violations.ShouldNotBeEmpty();
        violations.ShouldContain(v => v.BranchName == "master");
        violations.ShouldContain(v => v.BranchName == "release");
        violations.ShouldAllBe(v => v.Severity == SemanticViolationSeverity.Error);
    }

    [TestCase(PrimsFoundationYaml, TestName = "Foundation")]
    [TestCase(PrimsStrataYaml,     TestName = "Strata")]
    public void StaticLabelledBranches_EmitSem004Advisory_ContinuousDeploymentIntentClarification(string yaml)
    {
        // SEM-004 is Advisory: explains that ContinuousDeployment + non-empty label produces
        // prerelease output but treats every commit as deployable. No broken behaviour.
        // {BranchName} labels on feature/bugfix/hotfix are suppressed — CD is intentional there.
        var sem004 = SemanticValidator.Validate(yaml)
            .Where(v => v.RuleId == "SEM-004")
            .ToList();

        sem004.ShouldAllBe(v => v.Severity == SemanticViolationSeverity.Advisory);

        var branches = sem004.Select(v => v.BranchName).ToHashSet();
        branches.ShouldContain("develop", "static label 'alpha' — CD intent worth clarifying");
        branches.ShouldContain("release", "static label 'beta' — CD intent worth clarifying");
        branches.ShouldNotContain("master",  "empty label — no advisory needed");
        branches.ShouldNotContain("feature", "{BranchName} on short-lived branch — advisory suppressed");
        branches.ShouldNotContain("bugfix",  "{BranchName} on short-lived branch — advisory suppressed");
        // hotfix: Foundation has {BranchName} (suppressed); Strata has static 'beta' (advisory fires)
    }

    [TestCase(PrimsFoundationYaml, TestName = "Foundation")]
    [TestCase(PrimsStrataYaml,     TestName = "Strata")]
    public void ViolatesSem006_NoStrategiesDeclared(string yaml)
    {
        var violations = SemanticValidator.Validate(yaml).Where(v => v.RuleId == "SEM-006").ToList();

        violations.Count.ShouldBe(1);
        violations[0].BranchName.ShouldBeNull();
        violations[0].Severity.ShouldBe(SemanticViolationSeverity.Advisory);
    }

    [TestCase(PrimsFoundationYaml, TestName = "Foundation")]
    [TestCase(PrimsStrataYaml,     TestName = "Strata")]
    public void InheritBranches_ViolateSem007_NoSourceBranches(string yaml)
    {
        var branches = SemanticValidator.Validate(yaml)
            .Where(v => v.RuleId == "SEM-007")
            .Select(v => v.BranchName)
            .ToHashSet();

        branches.ShouldContain("feature");
        branches.ShouldContain("bugfix");
        branches.ShouldNotContain("hotfix",  "increment: Patch");
        branches.ShouldNotContain("develop", "increment: Minor");
        branches.ShouldNotContain("release", "increment: None");
    }

    [TestCase(PrimsFoundationYaml, TestName = "Foundation")]
    [TestCase(PrimsStrataYaml,     TestName = "Strata")]
    public void ViolationSet_ContainsExpectedRulesAndNoOthers(string yaml)
    {
        var rules = RuleIds(yaml);

        rules.ShouldContain("SEM-001");
        rules.ShouldContain("SEM-004");
        rules.ShouldContain("SEM-006");
        rules.ShouldContain("SEM-007");
        rules.ShouldNotContain("SEM-002", "no is-main-branch: true declared");
        rules.ShouldNotContain("SEM-003", "all {BranchName} labels have capture groups");
        rules.ShouldNotContain("SEM-005", "no source-branches listed to reference-check");
        // SEM-004 fires as Advisory — included in rule set but at advisory severity
        SemanticValidator.Validate(yaml)
            .Where(v => v.RuleId == "SEM-004")
            .ShouldAllBe(v => v.Severity == SemanticViolationSeverity.Advisory);
    }

    private const string PrimsFoundationYaml = """
        mode: ContinuousDeployment
        branches:
          master:
            regex: ^master$
            label: ''
            increment: Patch
            prevent-increment:
              of-merged-branch: true
            track-merge-target: false
            is-release-branch: true
          develop:
            regex: ^develop$
            label: alpha
            increment: Minor
            prevent-increment:
              of-merged-branch: false
            track-merge-target: true
            is-release-branch: false
          release:
            regex: ^release/
            label: beta
            increment: None
            prevent-increment:
              of-merged-branch: true
            track-merge-target: false
            is-release-branch: true
          feature:
            regex: ^feature/(?<BranchName>.+)
            label: '{BranchName}'
            increment: Inherit
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
          bugfix:
            regex: ^bugfix/(?<BranchName>.+)
            label: '{BranchName}'
            increment: Inherit
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
          hotfix:
            regex: ^hotfix/(?<BranchName>.+)
            label: '{BranchName}'
            increment: Patch
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
        ignore:
          sha: []
        """;

    private const string PrimsStrataYaml = """
        mode: ContinuousDeployment
        branches:
          master:
            regex: ^master$
            label: ''
            increment: Patch
            prevent-increment:
              of-merged-branch: true
            track-merge-target: false
            is-release-branch: true
          develop:
            regex: ^develop$
            label: alpha
            increment: Minor
            prevent-increment:
              of-merged-branch: false
            track-merge-target: true
            is-release-branch: false
          release:
            regex: ^release/
            label: beta
            increment: None
            prevent-increment:
              of-merged-branch: true
            track-merge-target: false
            is-release-branch: true
          feature:
            regex: ^feature/(?<BranchName>.+)
            label: '{BranchName}'
            increment: Inherit
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
          bugfix:
            regex: ^bugfix/(?<BranchName>.+)
            label: '{BranchName}'
            increment: Inherit
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
          hotfix:
            regex: ^hotfix/
            label: beta
            increment: Patch
            prevent-increment:
              of-merged-branch: false
            track-merge-target: false
            is-release-branch: false
        ignore:
          sha: []
        """;
}
