namespace GitVersion.Configuration;

public interface IConfigurationProvider
{
    IGitVersionConfiguration Provide(IReadOnlyDictionary<object, object?>? overrideConfiguration = null);

    /// <summary>
    /// Returns the per-source provenance information used to build the effective
    /// configuration: the workflow name (if any) and the raw override dictionaries
    /// from the user's config file, the workflow, and the <c>/overrideconfig</c>
    /// CLI parameter. Consumed by <c>gitversion /validate /explain</c> to attribute
    /// validator findings to the configuration source that supplied each field.
    /// </summary>
    ConfigurationProvenance ResolveProvenance();
}
