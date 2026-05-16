using System.IO.Abstractions;
using GitVersion.Core.Tests.Helpers;
using GitVersion.Helpers;

namespace GitVersion.Configuration.Tests;

/// <summary>
/// Unit tests for <see cref="IConfigurationProvider.ResolveProvenance"/>.
/// Each scenario class arranges a working directory once in
/// <see cref="OneTimeSetUpAttribute"/>, resolves provenance, and asserts
/// individual observables per test.
/// </summary>
/// <remarks>
/// Atomic, idempotent, environment-independent: each scenario creates a
/// fresh temp directory under <see cref="System.IO.Path.GetTempPath"/>
/// and tears it down in <see cref="OneTimeTearDownAttribute"/>.
/// </remarks>
public static class ConfigurationProvenance_Behaviour
{
    public abstract class ScenarioFixture : TestBase
    {
        protected string RepoPath = null!;
        protected ConfigurationProvenance Provenance = null!;

        protected virtual string? ConfigYaml => null;
        protected virtual IReadOnlyDictionary<object, object?>? CliOverride => null;

        [OneTimeSetUp]
        public void Arrange_Act()
        {
            // Path.GetTempPath() honours TMPDIR; under the test runner that
            // resolves to the bin directory inside the repo, so FindGitDir
            // walks up to the repo's .git and ResolveProvenance reads the
            // repo's own .gitversion.yml. Use /tmp literally to land outside
            // any git tree.
            RepoPath = FileSystemHelper.Path.Combine("/tmp",
                "gv-provenance-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RepoPath);

            if (ConfigYaml is not null)
                File.WriteAllText(FileSystemHelper.Path.Combine(RepoPath, "GitVersion.yml"), ConfigYaml);

            var options = Options.Create(new GitVersionOptions
            {
                WorkingDirectory = RepoPath,
                ConfigurationInfo = { OverrideConfiguration = CliOverride }
            });
            // Bind the open IOptions<T> interface explicitly so the provider's
            // IOptions<GitVersionOptions> dependency resolves to our test
            // instance rather than the default-constructed one (which would
            // pick up SysEnv.CurrentDirectory and walk into the repo's own
            // .gitversion.yml).
            var sp = ConfigureServices(services => services.AddSingleton<IOptions<GitVersionOptions>>(options));
            var provider = sp.GetRequiredService<IConfigurationProvider>();
            Provenance = provider.ResolveProvenance();
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(RepoPath))
                Directory.Delete(RepoPath, recursive: true);
        }
    }

    [TestFixture]
    public class WhenNoConfigFileAndNoCliOverride : ScenarioFixture
    {
        [Test] public void Workflow_IsNull() => Provenance.Workflow.ShouldBeNull();
        [Test] public void FromFile_IsNull() => Provenance.FromFile.ShouldBeNull();
        [Test] public void FromWorkflow_IsNull() => Provenance.FromWorkflow.ShouldBeNull();
        [Test] public void FromCliOverride_IsNull() => Provenance.FromCliOverride.ShouldBeNull();
    }

    [TestFixture]
    public class WhenConfigFileDeclaresWorkflow : ScenarioFixture
    {
        protected override string ConfigYaml =>
            """
            workflow: TrunkBased/preview1
            branches:
              feature:
                regex: '^my-features?/.+$'
            """;

        [Test] public void Workflow_IsCapturedFromFile() => Provenance.Workflow.ShouldBe("TrunkBased/preview1");
        [Test] public void FromFile_IsPopulated() => Provenance.FromFile.ShouldNotBeNull();
        [Test] public void FromFile_ContainsBranchesKey() => Provenance.FromFile!.ShouldContainKey("branches");
        [Test] public void FromWorkflow_IsPopulated() => Provenance.FromWorkflow.ShouldNotBeNull();
        [Test] public void FromCliOverride_IsNull() => Provenance.FromCliOverride.ShouldBeNull();
    }

    [TestFixture]
    public class WhenCliOverrideSupplied : ScenarioFixture
    {
        protected override IReadOnlyDictionary<object, object?>? CliOverride =>
            new Dictionary<object, object?> { ["tag-prefix"] = "[abc]" };

        [Test] public void FromCliOverride_IsPopulated() => Provenance.FromCliOverride.ShouldNotBeNull();
        [Test] public void FromCliOverride_ContainsTagPrefixKey() => Provenance.FromCliOverride!.ShouldContainKey("tag-prefix");
        [Test] public void FromFile_IsNull() => Provenance.FromFile.ShouldBeNull();
    }
}
