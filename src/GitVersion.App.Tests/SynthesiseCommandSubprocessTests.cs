using GitVersion.App.Tests.Helpers;
using GitVersion.Helpers;

namespace GitVersion.App.Tests;

/// <summary>
/// Out-of-process wire validation for <c>gitversion /synthesise</c>. Confirms
/// the argument-parser → executor → exit-code chain is wired through when the
/// CLI binary is invoked. Detailed observable shape is owned by the
/// in-process tests in <see cref="SynthesiseCommand"/>; this fixture only
/// asserts the binary contract.
/// </summary>
public static class SynthesiseCommandSubprocess
{
    private const string ValidTrunkBasedIntake =
        """
        {
          "incrementSource": "BranchName",
          "branches": [
            { "pattern": "master",         "example": "1.0.0" },
            { "pattern": "feature/Login",  "example": "1.1.0-Login.1" }
          ]
        }
        """;

    private const string AmbiguousIntakeMissingAuthority =
        """
        {
          "incrementSource": "BranchName",
          "branches": [
            { "pattern": "develop",        "example": "1.0.0-alpha.1" },
            { "pattern": "feature/Login",  "example": "1.0.0-Login.1" }
          ]
        }
        """;

    public abstract class ScenarioFixture
    {
        protected string TempDir = null!;
        protected ExecutionResults Result = null!;

        protected abstract string IntakeJson { get; }

        [OneTimeSetUp]
        public void Arrange_Act()
        {
            TempDir = FileSystemHelper.Path.Combine(FileSystemHelper.Path.GetTempPath(),
                "gv-synthesise-subproc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            File.WriteAllText(FileSystemHelper.Path.Combine(TempDir, "intake.json"), IntakeJson);

            // Use a relative intake filename; the subprocess inherits TempDir as
            // its working directory via ExecuteIn, so File.Exists resolves
            // intake.json against that directory. Avoids quoting an absolute
            // path through the shell layer.
            Result = GitVersionHelper.ExecuteIn(TempDir, " /synthesise /intake intake.json", logToFile: false);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
    }

    [TestFixture]
    public class WhenSubprocessSynthesisesCleanIntake : ScenarioFixture
    {
        protected override string IntakeJson => ValidTrunkBasedIntake;

        // The CLI parser defaults Output to JSON when no -output is supplied,
        // so the binary emits the {yaml, diagnostics} JSON envelope. Assert
        // against that envelope shape.

        [Test] public void ExitCode_IsZero() => Result.ExitCode.ShouldBe(0);
        [Test] public void Output_ContainsYamlField() => Result.Output!.ShouldContain("\"yaml\":");
        [Test] public void Output_ContainsDiagnosticsField() => Result.Output!.ShouldContain("\"diagnostics\":");
    }

    [TestFixture]
    public class WhenSubprocessSynthesisesAmbiguousIntake : ScenarioFixture
    {
        protected override string IntakeJson => AmbiguousIntakeMissingAuthority;

        [Test] public void ExitCode_IsOne() => Result.ExitCode.ShouldBe(1);
        [Test] public void Output_ContainsF001Diagnostic() => Result.Output!.ShouldContain("F-001");
    }
}
