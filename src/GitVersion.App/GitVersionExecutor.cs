using System.IO.Abstractions;
using System.Text.Json;
using GitVersion.Configuration;
using GitVersion.Configuration.Validation;
using GitVersion.Extensions;
using GitVersion.Git;
using GitVersion.Helpers;
using GitVersion.Logging;
using GitVersion.OutputVariables;

namespace GitVersion;

internal class GitVersionExecutor(
    ILog log,
    IFileSystem fileSystem,
    IConsole console,
    IConfigurationFileLocator configurationFileLocator,
    IConfigurationProvider configurationProvider,
    IConfigurationSerializer configurationSerializer,
    IGitVersionCalculateTool gitVersionCalculateTool,
    IGitVersionOutputTool gitVersionOutputTool,
    IGitRepository gitRepository,
    IGitRepositoryInfo repositoryInfo)
    : IGitVersionExecutor
{
    private readonly ILog log = log.NotNull();
    private readonly IFileSystem fileSystem = fileSystem.NotNull();
    private readonly IConsole console = console.NotNull();

    private readonly IConfigurationFileLocator configurationFileLocator = configurationFileLocator.NotNull();
    private readonly IConfigurationProvider configurationProvider = configurationProvider.NotNull();
    private readonly IConfigurationSerializer configurationSerializer = configurationSerializer.NotNull();

    private readonly IGitVersionCalculateTool gitVersionCalculateTool = gitVersionCalculateTool.NotNull();
    private readonly IGitVersionOutputTool gitVersionOutputTool = gitVersionOutputTool.NotNull();
    private readonly IGitRepository gitRepository = gitRepository.NotNull();
    private readonly IGitRepositoryInfo repositoryInfo = repositoryInfo.NotNull();

    public int Execute(GitVersionOptions gitVersionOptions)
    {
        Initialize(gitVersionOptions);

        if (gitVersionOptions.ConfigurationInfo.ValidateConfiguration)
            return RunValidation(gitVersionOptions);

        var exitCode = !VerifyAndDisplayConfiguration(gitVersionOptions)
            ? RunGitVersionTool(gitVersionOptions)
            : 0;

        if (exitCode != 0)
        {
            // Dump log to console if we fail to complete successfully
            this.console.Write(this.log.ToString());
        }

        return exitCode;
    }

    private int RunValidation(GitVersionOptions gitVersionOptions)
    {
        IGitVersionConfiguration configuration;
        try
        {
            configuration = this.configurationProvider.Provide(gitVersionOptions.ConfigurationInfo.OverrideConfiguration);
        }
        catch (Exception ex)
        {
            this.console.WriteLine($"GitVersion Semantic Configuration Validator{FileSystemHelper.Path.NewLine}");
            this.console.WriteLine($"❌  Error loading configuration: {ex.Message}");
            this.console.WriteLine($"{FileSystemHelper.Path.NewLine}─────────────────────────────────────────────────────────────────");
            this.console.WriteLine("  Configuration could not be loaded.");
            return 1;
        }

        var violations = new ConfigurationSemanticValidator().Validate(configuration);
        var errors = violations.Count(v => v.Severity == SemanticViolationSeverity.Error);
        var warnings = violations.Count(v => v.Severity == SemanticViolationSeverity.Warning);
        var advisories = violations.Count(v => v.Severity == SemanticViolationSeverity.Advisory);

        if (gitVersionOptions.Output.Contains(OutputType.Json))
        {
            EmitJson(violations, errors, warnings, advisories);
        }
        else
        {
            EmitText(violations, errors, warnings, advisories);
        }

        return errors > 0 ? 1 : 0;
    }

    private void EmitText(IReadOnlyList<SemanticViolation> violations, int errors, int warnings, int advisories)
    {
        this.console.WriteLine($"GitVersion Semantic Configuration Validator{FileSystemHelper.Path.NewLine}");

        foreach (var v in violations)
        {
            var icon = v.Severity switch
            {
                SemanticViolationSeverity.Error => "❌ ",
                SemanticViolationSeverity.Warning => "⚠  ",
                _ => "ℹ  "
            };
            var branch = v.BranchName != null ? $"  branch '{v.BranchName}'" : "  (root)";
            this.console.WriteLine($"{icon} {v.RuleId}  {v.Severity,-8}{branch}");
            this.console.WriteLine($"    {v.Title}");
            this.console.WriteLine($"    {v.Message}");
            this.console.WriteLine($"{FileSystemHelper.Path.NewLine}    Remediation: {v.Remediation}");
            if (!string.IsNullOrEmpty(v.CausalNote))
                this.console.WriteLine($"    Note: {v.CausalNote}");
            this.console.WriteLine(string.Empty);
        }

        this.console.WriteLine("─────────────────────────────────────────────────────────────────");

        if (errors == 0 && warnings == 0 && advisories == 0)
        {
            this.console.WriteLine("  All semantic invariants satisfied.");
            this.console.WriteLine($"{FileSystemHelper.Path.NewLine}  0 errors · 0 warnings · 0 advisories");
            this.console.WriteLine("  Configuration is semantically valid.");
        }
        else
        {
            var summary = $"  {errors} error{(errors == 1 ? "" : "s")} · {warnings} warning{(warnings == 1 ? "" : "s")} · {advisories} advisor{(advisories == 1 ? "y" : "ies")}";
            this.console.WriteLine(summary);
            this.console.WriteLine(errors > 0
                ? "  Configuration is semantically invalid. Fix errors before relying on output."
                : "  Configuration is semantically valid.");
        }
    }

    private void EmitJson(IReadOnlyList<SemanticViolation> violations, int errors, int warnings, int advisories)
    {
        var result = new
        {
            valid = errors == 0,
            summary = new { errors, warnings, advisories },
            violations = violations.Select(v => new
            {
                ruleId = v.RuleId,
                title = v.Title,
                severity = v.Severity.ToString(),
                branchName = v.BranchName,
                message = v.Message,
                remediation = v.Remediation,
                causalNote = v.CausalNote
            })
        };

        this.console.WriteLine(JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    private int RunGitVersionTool(GitVersionOptions gitVersionOptions)
    {
        this.gitRepository.DiscoverRepository(gitVersionOptions.WorkingDirectory);
        var mutexName = this.repositoryInfo.DotGitDirectory?.Replace(FileSystemHelper.Path.DirectorySeparatorChar.ToString(), "") ?? string.Empty;
        using var mutex = new Mutex(true, $@"Global\gitversion{mutexName}", out var acquired);

        try
        {
            if (!acquired)
            {
                mutex.WaitOne();
            }

            var variables = this.gitVersionCalculateTool.CalculateVersionVariables();

            var configuration = this.configurationProvider.Provide(gitVersionOptions.ConfigurationInfo.OverrideConfiguration);

            this.gitVersionOutputTool.OutputVariables(variables, configuration.UpdateBuildNumber);
            this.gitVersionOutputTool.UpdateAssemblyInfo(variables);
            this.gitVersionOutputTool.UpdateWixVersionFile(variables);
        }
        catch (WarningException exception)
        {
            var error = $"An error occurred:{FileSystemHelper.Path.NewLine}{exception.Message}";
            this.log.Warning(error);
            return 1;
        }
        catch (Exception exception)
        {
            var error = $"An unexpected error occurred:{FileSystemHelper.Path.NewLine}{exception}";
            this.log.Error(error);

            try
            {
                GitExtensions.DumpGraphLog(logMessage => this.log.Info(logMessage));
            }
            catch (Exception dumpGraphException)
            {
                this.log.Error($"Couldn't dump the git graph due to the following error: {dumpGraphException}");
            }
            return 1;
        }
        finally
        {
            mutex.ReleaseMutex();
        }

        return 0;
    }

    private void Initialize(GitVersionOptions gitVersionOptions)
    {
        if (gitVersionOptions.Diag)
        {
            gitVersionOptions.Settings.NoCache = true;
        }

        if (gitVersionOptions.Output.Contains(OutputType.BuildServer) || gitVersionOptions.LogFilePath == "console")
        {
            this.log.AddLogAppender(new ConsoleAppender());
        }

        if (gitVersionOptions.LogFilePath != null && gitVersionOptions.LogFilePath != "console")
        {
            this.log.AddLogAppender(new FileAppender(this.fileSystem, gitVersionOptions.LogFilePath));
        }

        var workingDirectory = gitVersionOptions.WorkingDirectory;
        if (gitVersionOptions.Diag)
        {
            GitExtensions.DumpGraphLog(logMessage => this.log.Info(logMessage));
        }

        if (!this.fileSystem.Directory.Exists(workingDirectory))
        {
            this.log.Warning($"The working directory '{workingDirectory}' does not exist.");
        }
        else
        {
            this.log.Info("Working directory: " + workingDirectory);
        }
    }

    private bool VerifyAndDisplayConfiguration(GitVersionOptions gitVersionOptions)
    {
        if (!gitVersionOptions.ConfigurationInfo.ShowConfiguration) return false;
        if (gitVersionOptions.RepositoryInfo.TargetUrl.IsNullOrWhiteSpace())
        {
            this.configurationFileLocator.Verify(gitVersionOptions.WorkingDirectory, this.repositoryInfo.ProjectRootDirectory);
        }

        var configuration = this.configurationProvider.Provide();
        var configurationString = this.configurationSerializer.Serialize(configuration);
        this.console.WriteLine(configurationString);
        return true;
    }
}
