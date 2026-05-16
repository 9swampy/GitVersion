namespace GitVersion.Configuration;

/// <summary>
/// Captures the three raw override sources that <see cref="IConfigurationProvider"/>
/// merges into the effective <see cref="IGitVersionConfiguration"/>, plus the
/// workflow name (if any) the user selected. Consumed by <c>gitversion /validate
/// /explain</c> to attribute each violation field to its source.
/// </summary>
/// <param name="Workflow">
/// The workflow string declared by the user (e.g. <c>"TrunkBased/preview1"</c>),
/// or <c>null</c> when no workflow was selected.
/// </param>
/// <param name="FromFile">
/// The user's <c>GitVersion.yml</c> contents as a raw dictionary, or <c>null</c>
/// when no config file was found.
/// </param>
/// <param name="FromWorkflow">
/// The workflow's contribution as a raw dictionary, or <c>null</c> when no
/// workflow was selected.
/// </param>
/// <param name="FromCliOverride">
/// The <c>/overrideconfig</c> contribution as a raw dictionary, or <c>null</c>
/// when none was supplied.
/// </param>
/// <remarks>
/// Top-level dictionary keys are <see cref="string"/>; nested values may
/// themselves be dictionaries (consumers navigating to
/// <c>branches.&lt;name&gt;.&lt;field&gt;</c> should expect <see cref="object"/>
/// at intermediate levels and cast as required).
/// </remarks>
public sealed record ConfigurationProvenance(
    string? Workflow,
    IReadOnlyDictionary<string, object?>? FromFile,
    IReadOnlyDictionary<string, object?>? FromWorkflow,
    IReadOnlyDictionary<string, object?>? FromCliOverride);
