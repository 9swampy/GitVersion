using GitVersion.Configuration;
using GitVersion.Configuration.Validation;

namespace GitVersion.Core.Tests.Configuration;

internal static class SemanticValidator
{
    internal static IReadOnlyList<SemanticViolation> Validate(string yaml) =>
        new ConfigurationSemanticValidator().Validate(
            new ConfigurationSerializer().ReadConfiguration(yaml)!);
}
