namespace GitVersion;

public record ConfigurationInfo
{
    public string? ConfigurationFile;
    public bool ShowConfiguration;
    public bool ValidateConfiguration;
    public IReadOnlyDictionary<object, object?>? OverrideConfiguration;
}
