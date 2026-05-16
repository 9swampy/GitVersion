namespace GitVersion;

public record ConfigurationInfo
{
    public string? ConfigurationFile;
    public bool ShowConfiguration;
    public bool ValidateConfiguration;
    public bool SynthesiseConfiguration;
    public string? SynthesiseIntakeFile;
    public bool ExplainProvenance;
    public IReadOnlyDictionary<object, object?>? OverrideConfiguration;
}
