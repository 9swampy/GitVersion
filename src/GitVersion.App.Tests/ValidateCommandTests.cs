using System.Text.Json.Nodes;
using GitVersion.Core.Tests.Helpers;
using GitVersion.Extensions;
using GitVersion.Helpers;
using GitVersion.Logging;

namespace GitVersion.App.Tests;

/// <summary>
/// In-process integration tests for <c>gitversion /validate</c>. Each scenario fixture
/// arranges its input and runs the validator once in <see cref="OneTimeSetUpAttribute"/>;
/// every assertion is its own <see cref="TestAttribute"/> against the captured state.
/// </summary>
/// <remarks>
/// Atomic, idempotent, environment-independent: each scenario class creates a fresh
/// temp directory under <see cref="System.IO.Path.GetTempPath"/>, writes its own
/// <c>GitVersion.yml</c>, and removes the directory in <see cref="OneTimeTearDownAttribute"/>.
/// No reliance on the host's working directory, repo layout, or CI environment variables.
/// </remarks>
public static class ValidateCommand
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

    // SEM-001 trigger: master claims release authority but its regex carries no version pattern,
    // so GitVersion's version-from-branch-name path can never succeed. Error severity.
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
        protected StringBuilder ConsoleBuffer = null!;
        protected int ExitCode;

        protected string Output => ConsoleBuffer.ToString();

        protected abstract string Yaml { get; }
        protected virtual bool EmitAsJson => false;

        [OneTimeSetUp]
        public void Arrange_Act()
        {
            TempDir = FileSystemHelper.Path.Combine(FileSystemHelper.Path.GetTempPath(),
                "gv-validate-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDir);
            File.WriteAllText(FileSystemHelper.Path.Combine(TempDir, "GitVersion.yml"), Yaml);

            ConsoleBuffer = new StringBuilder();

            // Build GitVersionOptions directly. Going via GitVersionAppModule(args)
            // would route through the CLI parser, which defaults Output to JSON when
            // no -output flag is present (ArgumentParser.cs:88-90) and would force
            // every scenario down the EmitJson path. The test surface here is the
            // /validate executor wiring, not the parser's default-output behaviour
            // — that is exercised by ArgumentParserTests.ValidateSwitch_*.
            var options = Options.Create(new GitVersionOptions
            {
                WorkingDirectory = TempDir,
                ConfigurationInfo = { ValidateConfiguration = true },
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
    public class WhenConfigIsValid : ScenarioFixture
    {
        protected override string Yaml => ValidGitFlowYaml;

        [Test] public void ExitCode_IsZero() => ExitCode.ShouldBe(0);
        [Test] public void Output_AnnouncesValidator() => Output.ShouldContain("Semantic Configuration Validator");
        [Test] public void Output_DeclaresSemanticallyValid() => Output.ShouldContain("semantically valid");
        [Test] public void Output_ReportsZeroErrors() => Output.ShouldContain("0 errors");
        [Test] public void Output_ContainsNoErrorIcon() => Output.ShouldNotContain("❌");
    }

    [TestFixture]
    public class WhenConfigIsInvalid : ScenarioFixture
    {
        protected override string Yaml => InvalidSem001Yaml;

        [Test] public void ExitCode_IsOne() => ExitCode.ShouldBe(1);
        [Test] public void Output_AnnouncesValidator() => Output.ShouldContain("Semantic Configuration Validator");
        [Test] public void Output_DeclaresSemanticallyInvalid() => Output.ShouldContain("semantically invalid");
        [Test] public void Output_ContainsErrorIcon() => Output.ShouldContain("❌");
        [Test] public void Output_ContainsRuleIdSem001() => Output.ShouldContain("SEM-001");
        [Test] public void Output_IdentifiesOffendingBranch() => Output.ShouldContain("master");
        [Test] public void Output_IncludesRemediation() => Output.ShouldContain("Remediation:");
    }

    [TestFixture]
    public class WhenJsonOutputRequested : ScenarioFixture
    {
        protected override string Yaml => ValidGitFlowYaml;
        protected override bool EmitAsJson => true;

        private JsonNode Root => JsonNode.Parse(Output)!;

        [Test] public void ExitCode_IsZero() => ExitCode.ShouldBe(0);
        [Test] public void Output_ParsesAsJson() => Should.NotThrow(() => _ = JsonNode.Parse(Output));
        [Test] public void Json_ValidIsTrue() => Root["valid"]!.GetValue<bool>().ShouldBeTrue();
        [Test] public void Json_SummaryErrorsIsZero() => Root["summary"]!["errors"]!.GetValue<int>().ShouldBe(0);
        [Test] public void Json_ViolationsArrayIsEmpty() => Root["violations"]!.AsArray().Count.ShouldBe(0);
    }
}
