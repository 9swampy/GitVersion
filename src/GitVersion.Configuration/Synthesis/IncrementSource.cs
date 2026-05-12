namespace GitVersion.Configuration.Synthesis;

/// <summary>
/// The mechanism by which the version number advances — the one forced-choice
/// question that examples alone cannot answer.
/// </summary>
public enum IncrementSource
{
    /// <summary>Version authority branch names drive increments (e.g. release/1.62.0).</summary>
    BranchName,

    /// <summary>Tags on primary branches are the sole version source.</summary>
    TagOnly,

    /// <summary>Commit message directives (+semver: minor/major/patch) drive increments.</summary>
    CommitMessage,

    /// <summary>Both branch name authority and commit message directives are honoured.</summary>
    BranchNameAndCommitMessage
}
