using System.Text.Json.Nodes;
using GitVersion.Core.Tests.Helpers;
using GitVersion.Extensions;
using GitVersion.Helpers;
using GitVersion.Logging;

namespace GitVersion.App.Tests;

/// <summary>
/// In-process integration tests for <c>gitversion /synthesise</c>. Each scenario
/// fixture writes its own intake JSON under <see cref="System.IO.Path.GetTempPath"/>,
/// runs the executor once in <see cref="OneTimeSetUpAttribute"/>, and captures
/// (exit code, console output). Every <see cref="TestAttribute"/> asserts one
/// binary-falsifiable observable.
/// </summary>
/// <remarks>
/// Atomic, idempotent, environment-independent: per-scenario temp dir under
/// <c>Path.GetTempPath()</c>, no reliance on host paths, repo layout, or CI
/// environment variables.
/// </remarks>
public static class SynthesiseCommand
{
    private const string ValidTrunkBasedIntake =
        """
        {
          "incrementSource": "BranchName",
          "branches": [
            { "pattern": "master",          "example": "1.0.0" },
            { "pattern": "feature/Login",   "example": "1.1.0-Login.1" }
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

    private const string DuplicateFamilyIntake =
        """
        {
          "incrementSource": "BranchName",
          "branches": [
            { "pattern": "master",         "example": "1.0.0" },
            { "pattern": "release/1.0.0",  "example": "1.0.0-beta.1" },
            { "pattern": "feature/Login",  "example": "1.0.0-Login.1" },
            { "pattern": "feature/Search", "example": "1.0.0-Search.1" }
          ]
        }
        """;

    public abstract class ScenarioFixture
    {
        protected string TempDir = null!;
        protected string IntakePath = null!;
        protected StringBuilder ConsoleBuffer = null!;
        protected int ExitCode;

        protected string Output => ConsoleBuffer.ToString();

        protected abstract string? IntakeJson { get; }
        protected virtual bool WriteIntakeFile => true;
        protected virtual string IntakeFileName => "intake.json";
        protected virtual bool EmitAsJson => false;

        [OneTimeSetUp]
        public void Arrange_Act()
        {
            TempDir = FileSystemHelper.Path.Combine(FileSystemHelper.Path.GetTempPath(),
                "gv-synthesise-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            IntakePath = FileSystemHelper.Path.Combine(TempDir, IntakeFileName);
            if (WriteIntakeFile && IntakeJson is not null)
                File.WriteAllText(IntakePath, IntakeJson);

            ConsoleBuffer = new StringBuilder();

            var options = Options.Create(new GitVersionOptions
            {
                WorkingDirectory = TempDir,
                ConfigurationInfo =
                {
                    SynthesiseConfiguration = true,
                    SynthesiseIntakeFile = IntakePath
                },
                Output = EmitAsJson
                    ? new HashSet<OutputType> { OutputType.Json }
                    : new HashSet<OutputType>()
            });

            IServiceCollection services = new ServiceCollection();
            services.AddModule(new GitVersionCoreTestModule());
            services.AddModule(new GitVersionAppModule());
            services.AddSingleton(options);
            services.AddSingleton<IConsole>(new TestConsoleAdapter(ConsoleBuffer));

            using var sp = services.BuildServiceProvider();
            var executor = sp.GetRequiredService<IGitVersionExecutor>();
            ExitCode = executor.Execute(options.Value);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
    }

    [TestFixture]
    public class WhenIntakeIsCleanAndTextRequested : ScenarioFixture
    {
        protected override string IntakeJson => ValidTrunkBasedIntake;

        [Test] public void ExitCode_IsZero() => ExitCode.ShouldBe(0);
        [Test] public void Output_BeginsWithAssemblyVersioningScheme() =>
            Output.TrimStart().ShouldStartWith("assembly-versioning-scheme:");
        [Test] public void Output_ContainsMasterAsMainBranch() =>
            Output.ShouldContain("is-main-branch: true");
        [Test] public void Output_ContainsFeatureBranchEntry() =>
            Output.ShouldContain("feature:");
    }

    [TestFixture]
    public class WhenIntakeIsCleanAndJsonRequested : ScenarioFixture
    {
        protected override string IntakeJson => ValidTrunkBasedIntake;
        protected override bool EmitAsJson => true;

        private JsonNode Root => JsonNode.Parse(Output)!;

        [Test] public void ExitCode_IsZero() => ExitCode.ShouldBe(0);
        [Test] public void Output_ParsesAsJson() => Should.NotThrow(() => _ = JsonNode.Parse(Output));
        [Test] public void Json_YamlIsNonEmpty() => Root["yaml"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
        [Test] public void Json_DiagnosticsArrayIsEmpty() => Root["diagnostics"]!.AsArray().Count.ShouldBe(0);
        [Test] public void Json_YamlContainsBranchesSection() => Root["yaml"]!.GetValue<string>().ShouldContain("branches:");
    }

    [TestFixture]
    public class WhenIntakeIsAmbiguous : ScenarioFixture
    {
        protected override string IntakeJson => AmbiguousIntakeMissingAuthority;

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_AnnouncesSynthesisHeader() =>
            Output.ShouldContain("GitVersion Configuration Synthesis");
        [Test] public void Output_ContainsF001Diagnostic() =>
            Output.ShouldContain("F-001");
        [Test] public void Output_ContainsErrorIcon() =>
            Output.ShouldContain("❌");
    }

    [TestFixture]
    public class WhenIntakeDuplicatesAFamily : ScenarioFixture
    {
        // Two feature/* entries → F-005 from AmbiguityDetector at intake time.
        protected override string IntakeJson => DuplicateFamilyIntake;

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_ContainsF005Diagnostic() =>
            Output.ShouldContain("F-005");
        [Test] public void Output_NamesTheCollidingFamily() =>
            Output.ShouldContain("feature");
    }

    [TestFixture]
    public class WhenIntakeFileIsMissing : ScenarioFixture
    {
        protected override string? IntakeJson => null;
        protected override bool WriteIntakeFile => false;

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_ReportsIntakeNotFound() =>
            Output.ShouldContain("intake file not found");
    }

    [TestFixture]
    public class WhenIntakeJsonIsMalformed : ScenarioFixture
    {
        protected override string IntakeJson => "{ this is not valid json";

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_ReportsMalformedJson() =>
            Output.ShouldContain("intake JSON malformed");
    }

    [TestFixture]
    public class WhenIntakeIsEmpty : ScenarioFixture
    {
        protected override string IntakeJson =>
            """
            { "incrementSource": "BranchName", "branches": [] }
            """;

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_RequiresAtLeastOneBranch() =>
            Output.ShouldContain("must declare at least one branch");
    }
}
