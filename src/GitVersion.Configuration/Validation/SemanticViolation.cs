namespace GitVersion.Configuration.Validation;

/// <param name="CausalNote">
/// Optional note explaining this violation's relationship to another violation on the same branch,
/// e.g. "This violation amplifies SEM-001: label-leak is compounded by missing source-branches: []."
/// Supports Refinement #1: structured diagnostic graph rather than flat error list.
/// </param>
public sealed record SemanticViolation(
    string RuleId,
    SemanticViolationSeverity Severity,
    string? BranchName,
    string Message,
    string Remediation,
    string? CausalNote = null);

public enum SemanticViolationSeverity
{
    Warning,
    Error
}
