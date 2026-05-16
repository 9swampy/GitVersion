using System.IO.Abstractions;
using GitVersion.Configuration.Workflows;
using GitVersion.Extensions;
using GitVersion.Logging;
using SharpYaml;

namespace GitVersion.Configuration;

internal class ConfigurationProvider(
    IConfigurationFileLocator configFileLocator,
    IFileSystem fileSystem,
    ILog log,
    IConfigurationSerializer configurationSerializer,
    IOptions<GitVersionOptions> options)
    : IConfigurationProvider
{
    private readonly IConfigurationFileLocator configFileLocator = configFileLocator.NotNull();
    private readonly IFileSystem fileSystem = fileSystem.NotNull();
    private readonly ILog log = log.NotNull();
    private readonly IConfigurationSerializer configurationSerializer = configurationSerializer.NotNull();
    private readonly IOptions<GitVersionOptions> options = options.NotNull();

    public IGitVersionConfiguration Provide(IReadOnlyDictionary<object, object?>? overrideConfiguration)
    {
        var gitVersionOptions = this.options.Value;
        var workingDirectory = gitVersionOptions.WorkingDirectory;
        var projectRootDirectory = this.fileSystem.FindGitDir(workingDirectory)?.WorkingTreeDirectory;

        var configurationFile = this.configFileLocator.GetConfigurationFile(workingDirectory)
                             ?? this.configFileLocator.GetConfigurationFile(projectRootDirectory);

        return configurationFile is not null
            ? ProvideConfiguration(configurationFile, overrideConfiguration)
            : ProvideForDirectory(null, overrideConfiguration);
    }

    public ConfigurationProvenance ResolveProvenance()
    {
        var gitVersionOptions = this.options.Value;
        var workingDirectory = gitVersionOptions.WorkingDirectory;
        var projectRootDirectory = this.fileSystem.FindGitDir(workingDirectory)?.WorkingTreeDirectory;

        var configurationFile = this.configFileLocator.GetConfigurationFile(workingDirectory)
                             ?? this.configFileLocator.GetConfigurationFile(projectRootDirectory);

        var fromFile = ReadOverrideConfiguration(configurationFile);
        var fromCli = gitVersionOptions.ConfigurationInfo.OverrideConfiguration;
        var workflow = GetWorkflow(fromCli, fromFile);
        var fromWorkflow = WorkflowManager.GetOverrideConfiguration(workflow);

        return new ConfigurationProvenance(
            Workflow: workflow,
            FromFile: NormaliseKeys(fromFile),
            FromWorkflow: NormaliseKeys(fromWorkflow),
            FromCliOverride: NormaliseKeys(fromCli));
    }

    /// <summary>
    /// YAML deserialisation produces dictionaries keyed by <see cref="object"/>;
    /// at runtime those keys are always strings. Cast at the top level so
    /// <see cref="ConfigurationProvenance"/> exposes a typed contract; nested
    /// values may themselves be dictionaries and remain typed as
    /// <see cref="object"/> for the consumer to navigate.
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? NormaliseKeys(IReadOnlyDictionary<object, object?>? source)
    {
        if (source is null) return null;
        var result = new Dictionary<string, object?>(source.Count);
        foreach (var (key, value) in source)
        {
            if (key is string s) result[s] = value;
        }
        return result;
    }

    internal IGitVersionConfiguration ProvideForDirectory(string? workingDirectory,
                                                          IReadOnlyDictionary<object, object?>? overrideConfiguration = null)
    {
        var configFilePath = this.configFileLocator.GetConfigurationFile(workingDirectory);
        return ProvideConfiguration(configFilePath, overrideConfiguration);
    }

    private IGitVersionConfiguration ProvideConfiguration(string? configFile,
                                                          IReadOnlyDictionary<object, object?>? overrideConfiguration = null)
    {
        var overrideConfigurationFromFile = ReadOverrideConfiguration(configFile);

        var workflow = GetWorkflow(overrideConfiguration, overrideConfigurationFromFile);

        IConfigurationBuilder configurationBuilder = (workflow is null)
            ? GitFlowConfigurationBuilder.New
            : ConfigurationBuilder.New;

        var overrideConfigurationFromWorkflow = WorkflowManager.GetOverrideConfiguration(workflow);
        foreach (var item in new[] { overrideConfigurationFromWorkflow, overrideConfigurationFromFile, overrideConfiguration })
        {
            if (item is not null) configurationBuilder.AddOverride(item);
        }

        try
        {
            return configurationBuilder.Build();
        }
        catch (YamlException exception)
        {
            var baseException = exception.GetBaseException();
            throw new WarningException(
                $"Could not build the configuration instance because following exception occurred: '{baseException.Message}' " +
                "Please ensure that the /overrideconfig parameters are correct and the configuration file is in the correct format."
            );
        }
    }

    private Dictionary<object, object?>? ReadOverrideConfiguration(string? configFilePath)
    {
        if (configFilePath == null)
        {
            this.log.Info("No configuration file found, using default configuration");
            return null;
        }

        if (!this.fileSystem.File.Exists(configFilePath))
        {
            this.log.Info($"Configuration file '{configFilePath}' not found");
            return null;
        }

        this.log.Info($"Using configuration file '{configFilePath}'");
        var content = fileSystem.File.ReadAllText(configFilePath);
        return configurationSerializer.Deserialize<Dictionary<object, object?>>(content);
    }

    private static string? GetWorkflow(IReadOnlyDictionary<object, object?>? overrideConfiguration, IReadOnlyDictionary<object, object?>? overrideConfigurationFromFile)
    {
        string? workflow = null;
        foreach (var item in new[] { overrideConfigurationFromFile, overrideConfiguration })
        {
            if (item?.TryGetValue("workflow", out var value) == true && value != null)
            {
                workflow = (string)value;
            }
        }

        return workflow;
    }
}
