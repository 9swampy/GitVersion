using GitVersion.App.Tests.Helpers;
using GitVersion.Helpers;

namespace GitVersion.App.Tests;

/// <summary>
/// Out-of-process wire validation for <c>gitversion /validate</c>. These tests
/// confirm that the argument-parser → executor → exit-code chain is wired through
/// when the actual CLI binary is invoked — the parts the in-process tests in
/// <see cref="ValidateCommand"/> bypass by constructing <see cref="GitVersionOptions"/>
/// directly. Only the binary contract (exit code + a stable substring of output) is
/// asserted; detailed observable shape is owned by the in-process tests.
/// </summary>
/// <remarks>
/// Each scenario writes its own <c>GitVersion.yml</c> under <see cref="System.IO.Path.GetTempPath"/>,
/// invokes the CLI, and removes the temp directory in <see cref="OneTimeTearDownAttribute"/>.
/// Atomic, idempotent, environment-independent.
/// </remarks>
public static class ValidateCommandSubprocess
{
    private const string ValidGitFlowYaml =
        """
        assembly-versioning-scheme: MajorMinorPatch
        mode: ContinuousDeployment
        commit-message-incrementing: Enabled
        strategies:
          - Fallback
          - ConfiguredNextVersion
          - MergeMessage
          - TaggedCommit
          - TrackReleaseBranches
          - VersionInBranchName
        branches:
          master:
            regex: ^master$
            label: ''
            mode: ManualDeployment
            is-main-branch: true
            is-release-branch: false
            source-branches: []
        """;

    // SEM-001 trigger: see ValidateCommand.InvalidSem001Yaml for rationale.
    private const string InvalidSem001Yaml =
        """
        mode: ContinuousDeployment
        strategies:
          - Fallback
          - ConfiguredNextVersion
          - MergeMessage
          - TaggedCommit
          - TrackReleaseBranches
          - VersionInBranchName
        branches:
          master:
            regex: ^master$
            label: ''
            is-main-branch: true
            is-release-branch: true
            source-branches: []
        """;

    public abstract class ScenarioFixture
    {
        protected string TempDir = null!;
        protected ExecutionResults Result = null!;

        protected abstract string Yaml { get; }
        protected virtual string AdditionalArgs => string.Empty;

        [OneTimeSetUp]
        public void Arrange_Act()
        {
            TempDir = FileSystemHelper.Path.Combine(FileSystemHelper.Path.GetTempPath(),
                "gv-validate-subproc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            File.WriteAllText(FileSystemHelper.Path.Combine(TempDir, "GitVersion.yml"), Yaml);

            Result = GitVersionHelper.ExecuteIn(TempDir, $" /validate{AdditionalArgs}", logToFile: false);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
    }

    [TestFixture]
    public class WhenSubprocessValidatesValidConfig : ScenarioFixture
    {
        protected override string Yaml => ValidGitFlowYaml;

        // The CLI parser defaults Output to JSON when no -output flag is supplied,
        // so the binary emits JSON on the default path. Assert against the JSON
        // contract — the binary's actual default behaviour.

        [Test] public void ExitCode_IsZero() => Result.ExitCode.ShouldBe(0);
        [Test] public void Output_DeclaresValidTrue() => Result.Output!.ShouldContain("\"valid\": true");
    }

    [TestFixture]
    public class WhenSubprocessValidatesInvalidConfig : ScenarioFixture
    {
        protected override string Yaml => InvalidSem001Yaml;

        [Test] public void ExitCode_IsOne() => Result.ExitCode.ShouldBe(1);
        [Test] public void Output_DeclaresValidFalse() => Result.Output!.ShouldContain("\"valid\": false");
        [Test] public void Output_ContainsRuleIdSem001() => Result.Output!.ShouldContain("SEM-001");
    }

    [TestFixture]
    public class WhenSubprocessValidatesWithExplain : ScenarioFixture
    {
        protected override string Yaml => InvalidSem001Yaml;
        protected override string AdditionalArgs => " /explain";

        [Test] public void ExitCode_IsOne() => Result.ExitCode.ShouldBe(1);
        [Test] public void Output_ContainsSourceField() => Result.Output!.ShouldContain("\"source\":");
        [Test] public void Output_AttributesUserConfigOrigin() => Result.Output!.ShouldContain("set in your GitVersion.yml");
    }
}
