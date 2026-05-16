using System.IO.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitVersion.Configuration;
using GitVersion.Configuration.Synthesis;
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

        if (gitVersionOptions.ConfigurationInfo.SynthesiseConfiguration)
            return RunSynthesis(gitVersionOptions);

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

    private static readonly JsonSerializerOptions SynthesisJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Drives the detection-only synthesis pipeline from a JSON intake file and
    /// emits either the synthesised YAML or the structured diagnostics that
    /// blocked synthesis. Exit code 0 indicates a YAML was produced; exit code 1
    /// indicates the intake was malformed or under-determined and no YAML was
    /// emitted.
    /// </summary>
    private int RunSynthesis(GitVersionOptions gitVersionOptions)
    {
        var intakePath = gitVersionOptions.ConfigurationInfo.SynthesiseIntakeFile;
        if (string.IsNullOrEmpty(intakePath))
        {
            this.console.WriteLine(
                "gitversion /synthesise: required argument /intake <path> not provided.");
            return 1;
        }

        if (!this.fileSystem.File.Exists(intakePath))
        {
            this.console.WriteLine(
                $"gitversion /synthesise: intake file not found at '{intakePath}'.");
            return 1;
        }

        SynthesisIntake? intake;
        try
        {
            var json = this.fileSystem.File.ReadAllText(intakePath);
            intake = JsonSerializer.Deserialize<SynthesisIntake>(json, SynthesisJsonOptions);
        }
        catch (JsonException ex)
        {
            this.console.WriteLine(
                $"gitversion /synthesise: intake JSON malformed at '{intakePath}': {ex.Message}");
            return 1;
        }

        if (intake is null || intake.Branches is null || intake.Branches.Count == 0)
        {
            this.console.WriteLine(
                $"gitversion /synthesise: intake at '{intakePath}' must declare at least one branch.");
            return 1;
        }

        // System.Text.Json does not honour non-nullable record-parameter annotations
        // at deserialisation time: a missing "pattern" field yields a null Pattern.
        // Without this guard the null propagates to TopologyClassifier.Classify
        // and crashes with a raw ArgumentNullException before any diagnostic
        // surface can intercept. Surface the gap as a structured intake error.
        for (var i = 0; i < intake.Branches.Count; i++)
        {
            if (intake.Branches[i].Pattern is null)
            {
                this.console.WriteLine(
                    $"gitversion /synthesise: intake at '{intakePath}' has a branch entry " +
                    $"(index {i}) missing the required 'pattern' field.");
                return 1;
            }
        }

        var detection = new DetectionOnlySynthesis().Detect(
            intake.Branches.Select(b => (b.Pattern, b.Example)));

        if (!detection.IsSuccessful)
        {
            EmitSynthesisFailure(gitVersionOptions, detection.Diagnostics);
            return 1;
        }

        var synthConfig = new SemanticMapper().Map(detection, intake.IncrementSource);
        var yaml = new YamlEmitter().Emit(synthConfig);

        EmitSynthesisSuccess(gitVersionOptions, yaml);
        return 0;
    }

    private void EmitSynthesisSuccess(GitVersionOptions gitVersionOptions, string yaml)
    {
        if (gitVersionOptions.Output.Contains(OutputType.Json))
        {
            var payload = new
            {
                yaml,
                diagnostics = Array.Empty<object>()
            };
            this.console.WriteLine(JsonSerializer.Serialize(payload, SynthesisJsonOptions));
            return;
        }

        this.console.Write(yaml);
    }

    private void EmitSynthesisFailure(GitVersionOptions gitVersionOptions, IReadOnlyList<SynthesisDiagnostic> diagnostics)
    {
        if (gitVersionOptions.Output.Contains(OutputType.Json))
        {
            var payload = new
            {
                yaml = (string?)null,
                diagnostics = diagnostics.Select(d => new
                {
                    code = d.Code,
                    branchPattern = d.BranchPattern,
                    message = d.Message,
                    fields = d.Fields
                })
            };
            this.console.WriteLine(JsonSerializer.Serialize(payload, SynthesisJsonOptions));
            return;
        }

        this.console.WriteLine("GitVersion Configuration Synthesis" + FileSystemHelper.Path.NewLine);
        foreach (var d in diagnostics)
        {
            var branch = d.BranchPattern is not null ? $"  branch '{d.BranchPattern}'" : "  (intake)";
            this.console.WriteLine($"❌  {d.Code}{branch}");
            this.console.WriteLine(d.Message);
            this.console.WriteLine(string.Empty);
        }
        this.console.WriteLine("─────────────────────────────────────────────────────────────────");
        this.console.WriteLine($"  {diagnostics.Count} diagnostic{(diagnostics.Count == 1 ? "" : "s")} — no configuration synthesised.");
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

        // Resolve provenance only when /explain is in effect; otherwise the
        // text/JSON output shape is unchanged from the pre-Stack-4 contract.
        var provenance = gitVersionOptions.ConfigurationInfo.ExplainProvenance
            ? this.configurationProvider.ResolveProvenance()
            : null;

        if (gitVersionOptions.Output.Contains(OutputType.Json))
        {
            EmitJson(violations, errors, warnings, advisories, provenance);
        }
        else
        {
            EmitText(violations, errors, warnings, advisories, provenance);
        }

        return errors > 0 ? 1 : 0;
    }

    /// <summary>
    /// Maps each semantic rule to the field whose provenance most directly
    /// explains the violation. Hardcoded against the rule catalogue so the
    /// validator's <see cref="SemanticViolation"/> contract does not need to
    /// expand. SEM-006 is the only root-level rule; the rest are branch-level
    /// and report on a per-branch field.
    /// </summary>
    private static string? PrimaryFieldFor(string ruleId) => ruleId switch
    {
        "SEM-001" => "is-release-branch",
        "SEM-002" => "source-branches",
        "SEM-003" => "regex",
        "SEM-004" => "mode",
        "SEM-005" => "source-branches",
        "SEM-006" => "strategies",
        "SEM-007" => "source-branches",
        _ => null
    };

    /// <summary>
    /// Resolves the source of a (branchName, fieldName) pair against the
    /// raw override dictionaries captured by <see cref="ConfigurationProvenance"/>.
    /// Precedence is explicit and matches the merge order in
    /// ConfigurationProvider.ProvideConfiguration: CLI override → file →
    /// workflow → default. The first source that declares the field "wins".
    /// </summary>
    private static string ResolveSource(ConfigurationProvenance provenance, string? branchName, string? fieldName)
    {
        if (fieldName is null) return "from internal defaults";

        if (HasField(provenance.FromCliOverride, branchName, fieldName))
            return "set by /overrideconfig";
        if (HasField(provenance.FromFile, branchName, fieldName))
            return "set in your GitVersion.yml";
        if (HasField(provenance.FromWorkflow, branchName, fieldName))
            return $"inherited from workflow: {provenance.Workflow ?? "(unknown)"}";
        return "from internal defaults";
    }

    private static bool HasField(IReadOnlyDictionary<string, object?>? source, string? branchName, string fieldName)
    {
        if (source is null) return false;

        // Root-level field (branchName == null): look directly on the root.
        if (branchName is null) return source.ContainsKey(fieldName);

        // Branch-level field: navigate branches.<branchName>.<fieldName>.
        if (!source.TryGetValue("branches", out var branchesObj)) return false;
        if (branchesObj is not IDictionary<object, object?> branches) return false;
        if (!branches.TryGetValue(branchName, out var branchObj)) return false;
        if (branchObj is not IDictionary<object, object?> branch) return false;
        return branch.ContainsKey(fieldName);
    }

    private void EmitText(IReadOnlyList<SemanticViolation> violations, int errors, int warnings, int advisories, ConfigurationProvenance? provenance)
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
            if (provenance is not null)
            {
                var field = PrimaryFieldFor(v.RuleId);
                var source = ResolveSource(provenance, v.BranchName, field);
                var fieldLabel = field is not null ? $"{field} " : string.Empty;
                this.console.WriteLine($"    Source:      {fieldLabel}{source}");
            }
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

    private void EmitJson(IReadOnlyList<SemanticViolation> violations, int errors, int warnings, int advisories, ConfigurationProvenance? provenance)
    {
        // When /explain is in effect each violation gains a `source` object
        // ({ field, origin }); otherwise the violation shape is unchanged from
        // the pre-Stack-4 contract for backwards compatibility.
        var result = new
        {
            valid = errors == 0,
            summary = new { errors, warnings, advisories },
            violations = violations.Select(v =>
            {
                var field = provenance is not null ? PrimaryFieldFor(v.RuleId) : null;
                var origin = provenance is not null ? ResolveSource(provenance, v.BranchName, field) : null;
                return new
                {
                    ruleId = v.RuleId,
                    title = v.Title,
                    severity = v.Severity.ToString(),
                    branchName = v.BranchName,
                    message = v.Message,
                    remediation = v.Remediation,
                    causalNote = v.CausalNote,
                    source = provenance is null ? null : new { field, origin }
                };
            })
        };

        this.console.WriteLine(JsonSerializer.Serialize(result,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                // Omit null-valued properties so the `source` field appears
                // only when /explain is in effect, preserving the pre-Stack-4
                // JSON envelope shape for callers that do not opt in.
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            }));
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
