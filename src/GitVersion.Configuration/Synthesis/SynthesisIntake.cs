namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// User-supplied declarative intent for <c>gitversion /synthesise</c>. Deserialised
/// from the JSON file pointed at by <c>/intake &lt;path&gt;</c>.
/// </summary>
/// <param name="IncrementSource">
/// The Step-0 forced-choice intake answer for how versions advance
/// (commits, merges, branch-name authority, or tags). Cannot be inferred from
/// version examples alone.
/// </param>
/// <param name="Branches">One entry per branch pattern the user wants the
/// effective configuration to recognise. Each entry pairs the pattern with a
/// representative version example; the synthesis pipeline derives role,
/// label, mode, and regex from the pair.</param>
public sealed record SynthesisIntake(
    IncrementSource IncrementSource,
    IReadOnlyList<SynthesisIntakeBranch> Branches);

/// <summary>
/// One (branch pattern, version example) pair from a <see cref="SynthesisIntake"/>.
/// </summary>
/// <param name="Pattern">Branch name pattern as the user types it,
/// e.g. <c>"master"</c>, <c>"feature/Login"</c>, <c>"release/1.62.0"</c>.</param>
/// <param name="Example">Representative version output for that branch,
/// e.g. <c>"1.62.0"</c>, <c>"1.62.0-alpha.1243"</c>. May be null only when
/// the intent is purely topological (no example signal at all).</param>
public sealed record SynthesisIntakeBranch(string Pattern, string? Example);
