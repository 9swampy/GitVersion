using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace GitVersion.Core;

internal static partial class RegexPatterns
{
    private static readonly ConcurrentDictionary<string, Regex> Cache = new();

    public static Regex GetOrAddCachedRegex(string pattern) => Cache.GetOrAdd(
        pattern,
        _ => pattern switch
        {
            Common.SwitchArgumentRegexPattern => Common.SwitchArgumentRegex(),
            Common.ObscurePasswordRegexPattern => Common.ObscurePasswordRegex(),
            Common.ExpandTokensRegexPattern => Common.ExpandTokensRegex(),

            Configuration.DefaultTagPrefixRegexPattern => Configuration.DefaultTagPrefixRegex(),
            Configuration.DefaultVersionInBranchRegexPattern => Configuration.DefaultVersionInBranchRegex(),
            Configuration.MainBranchRegexPattern => Configuration.MainBranchRegex(),
            Configuration.DevelopBranchRegexPattern => Configuration.DevelopBranchRegex(),
            Configuration.ReleaseBranchRegexPattern => Configuration.ReleaseBranchRegex(),
            Configuration.FeatureBranchRegexPattern => Configuration.FeatureBranchRegex(),
            Configuration.PullRequestBranchRegexPattern => Configuration.PullRequestBranchRegex(),
            Configuration.HotfixBranchRegexPattern => Configuration.HotfixBranchRegex(),
            Configuration.SupportBranchRegexPattern => Configuration.SupportBranchRegex(),
            Configuration.UnknownBranchRegexPattern => Configuration.UnknownBranchRegex(),

            MergeMessage.DefaultMergeMessageRegexPattern => MergeMessage.DefaultMergeMessageRegex(),
            MergeMessage.SmartGitMergeMessageRegexPattern => MergeMessage.SmartGitMergeMessageRegex(),
            MergeMessage.BitBucketPullMergeMessageRegexPattern => MergeMessage.BitBucketPullMergeMessageRegex(),
            MergeMessage.BitBucketPullv7MergeMessageRegexPattern => MergeMessage.BitBucketPullv7MergeMessageRegex(),
            MergeMessage.BitBucketCloudPullMergeMessageRegexPattern => MergeMessage.BitBucketCloudPullMergeMessageRegex(),
            MergeMessage.GitHubPullMergeMessageRegexPattern => MergeMessage.GitHubPullMergeMessageRegex(),
            MergeMessage.RemoteTrackingMergeMessageRegexPattern => MergeMessage.RemoteTrackingMergeMessageRegex(),
            MergeMessage.AzureDevOpsPullMergeMessageRegexPattern => MergeMessage.AzureDevOpsPullMergeMessageRegex(),

            Output.AssemblyVersionRegexPattern => Output.AssemblyVersionRegex(),
            Output.AssemblyInfoVersionRegexPattern => Output.AssemblyInfoVersionRegex(),
            Output.AssemblyFileVersionRegexPattern => Output.AssemblyFileVersionRegex(),
            Output.CsharpAssemblyAttributeRegexPattern => Output.CsharpAssemblyAttributeRegex(),
            Output.FsharpAssemblyAttributeRegexPattern => Output.FsharpAssemblyAttributeRegex(),
            Output.VisualBasicAssemblyAttributeRegexPattern => Output.VisualBasicAssemblyAttributeRegex(),

            VersionCalculation.DefaultMajorRegexPattern => VersionCalculation.DefaultMajorRegex(),
            VersionCalculation.DefaultMinorRegexPattern => VersionCalculation.DefaultMinorRegex(),
            VersionCalculation.DefaultPatchRegexPattern => VersionCalculation.DefaultPatchRegex(),
            VersionCalculation.DefaultNoBumpRegexPattern => VersionCalculation.DefaultNoBumpRegex(),

            SemanticVersion.ParseStrictRegexPattern => SemanticVersion.ParseStrictRegex(),
            SemanticVersion.ParseLooseRegexPattern => SemanticVersion.ParseLooseRegex(),
            SemanticVersion.ParseBuildMetaDataRegexPattern => SemanticVersion.ParseBuildMetaDataRegex(),
            SemanticVersion.FormatBuildMetaDataRegexPattern => SemanticVersion.FormatBuildMetaDataRegex(),
            SemanticVersion.ParsePreReleaseTagRegexPattern => SemanticVersion.ParsePreReleaseTagRegex(),

            AssemblyVersion.CSharp.TriviaRegexPattern => AssemblyVersion.CSharp.TriviaRegex(),
            AssemblyVersion.CSharp.AttributeRegexPattern => AssemblyVersion.CSharp.AttributeRegex(),
            AssemblyVersion.FSharp.AttributeRegexPattern => AssemblyVersion.FSharp.AttributeRegex(),
            AssemblyVersion.VisualBasic.TriviaRegexPattern => AssemblyVersion.VisualBasic.TriviaRegex(),
            AssemblyVersion.VisualBasic.AttributeRegexPattern => AssemblyVersion.VisualBasic.AttributeRegex(),

            _ => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase),
        });

    internal static partial class Common
    {
        public const string SwitchArgumentRegexPattern = @"/\w+:";
        public const string ObscurePasswordRegexPattern = "(https?://)(.+)(:.+@)";
        public const string ExpandTokensRegexPattern = @"{((env:(?<envvar>\w+))|(?<member>\w+))(\s+(\?\?)??\s+((?<fallback>\w+)|""(?<fallback>.*)""))??}";

        [GeneratedRegex(SwitchArgumentRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex SwitchArgumentRegex();

        [GeneratedRegex(ObscurePasswordRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ObscurePasswordRegex();

        [GeneratedRegex(ExpandTokensRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ExpandTokensRegex();
    }

    internal static partial class Configuration
    {
        public const string DefaultTagPrefixRegexPattern = "[vV]?";
        public const string DefaultVersionInBranchRegexPattern = @"(?<version>[vV]?\d+(\.\d+)?(\.\d+)?).*";
        public const string MainBranchRegexPattern = "^master$|^main$";
        public const string DevelopBranchRegexPattern = "^dev(elop)?(ment)?$";
        public const string ReleaseBranchRegexPattern = @"^releases?[\\/-](?<BranchName>.+)";
        public const string FeatureBranchRegexPattern = @"^features?[\\/-](?<BranchName>.+)";
        public const string PullRequestBranchRegexPattern = "^(pull-requests|pull|pr)[\\/-](?<Number>\\d*)";
        public const string HotfixBranchRegexPattern = @"^hotfix(es)?[\\/-](?<BranchName>.+)";
        public const string SupportBranchRegexPattern = @"^support[\\/-](?<BranchName>.+)";
        public const string UnknownBranchRegexPattern = "(?<BranchName>.+)";

        [GeneratedRegex(DefaultTagPrefixRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultTagPrefixRegex();

        [GeneratedRegex(DefaultVersionInBranchRegexPattern)]
        public static partial Regex DefaultVersionInBranchRegex();

        [GeneratedRegex(MainBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex MainBranchRegex();

        [GeneratedRegex(DevelopBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DevelopBranchRegex();

        [GeneratedRegex(ReleaseBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ReleaseBranchRegex();

        [GeneratedRegex(FeatureBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex FeatureBranchRegex();

        [GeneratedRegex(PullRequestBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex PullRequestBranchRegex();

        [GeneratedRegex(HotfixBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex HotfixBranchRegex();

        [GeneratedRegex(SupportBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex SupportBranchRegex();

        [GeneratedRegex(UnknownBranchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex UnknownBranchRegex();
    }

    internal static partial class MergeMessage
    {
        public const string DefaultMergeMessageRegexPattern =
            @"^Merge (branch|tag) '(?<SourceBranch>[^']*)'(?: into (?<TargetBranch>[^\s]*))*";

        public const string SmartGitMergeMessageRegexPattern =
            @"^Finish (?<SourceBranch>[^\s]*)(?: into (?<TargetBranch>[^\s]*))*";

        public const string BitBucketPullMergeMessageRegexPattern =
            @"^Merge pull request #(?<PullRequestNumber>\d+) (from|in) (?<Source>.*) from (?<SourceBranch>[^\s]*) to (?<TargetBranch>[^\s]*)";

        public const string BitBucketPullv7MergeMessageRegexPattern =
            @"^Pull request #(?<PullRequestNumber>\d+).*\r?\n\r?\nMerge in (?<Source>.*) from (?<SourceBranch>[^\s]*) to (?<TargetBranch>[^\s]*)";

        public const string BitBucketCloudPullMergeMessageRegexPattern =
            @"^Merged in (?<SourceBranch>[^\s]*) \(pull request #(?<PullRequestNumber>\d+)\)";

        public const string GitHubPullMergeMessageRegexPattern =
            @"^Merge pull request #(?<PullRequestNumber>\d+) (from|in) (?:[^\s\/]+\/)?(?<SourceBranch>[^\s]*)(?: into (?<TargetBranch>[^\s]*))*";

        public const string RemoteTrackingMergeMessageRegexPattern =
            @"^Merge remote-tracking branch '(?<SourceBranch>[^\s]*)'(?: into (?<TargetBranch>[^\s]*))*";

        public const string AzureDevOpsPullMergeMessageRegexPattern =
            @"^Merge pull request (?<PullRequestNumber>\d+) from (?<SourceBranch>[^\s]*) into (?<TargetBranch>[^\s]*)";

        [GeneratedRegex(DefaultMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultMergeMessageRegex();

        [GeneratedRegex(SmartGitMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex SmartGitMergeMessageRegex();

        [GeneratedRegex(BitBucketPullMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex BitBucketPullMergeMessageRegex();

        [GeneratedRegex(BitBucketPullv7MergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex BitBucketPullv7MergeMessageRegex();

        [GeneratedRegex(BitBucketCloudPullMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex BitBucketCloudPullMergeMessageRegex();

        [GeneratedRegex(GitHubPullMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex GitHubPullMergeMessageRegex();

        [GeneratedRegex(RemoteTrackingMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex RemoteTrackingMergeMessageRegex();

        [GeneratedRegex(AzureDevOpsPullMergeMessageRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex AzureDevOpsPullMergeMessageRegex();
    }

    internal static partial class Output
    {
        public const string AssemblyVersionRegexPattern =
            @"AssemblyVersion(Attribute)?\s*\(.*\)\s*";

        public const string AssemblyInfoVersionRegexPattern =
            @"AssemblyInformationalVersion(Attribute)?\s*\(.*\)\s*";

        public const string AssemblyFileVersionRegexPattern =
            @"AssemblyFileVersion(Attribute)?\s*\(.*\)\s*";

        public const string CsharpAssemblyAttributeRegexPattern =
            @"(\s*\[\s*assembly:\s*(?:.*)\s*\]\s*$\r?\n?)";

        public const string FsharpAssemblyAttributeRegexPattern =
            @"(\s*\[\s*<assembly:\s*(?:.*)>\s*\]\s*$\r?\n?)";

        public const string VisualBasicAssemblyAttributeRegexPattern =
            @"(\s*<Assembly:\s*(?:.*)>\s*$\r?\n?)";

        [GeneratedRegex(AssemblyVersionRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex AssemblyVersionRegex();

        [GeneratedRegex(AssemblyInfoVersionRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex AssemblyInfoVersionRegex();

        [GeneratedRegex(AssemblyFileVersionRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex AssemblyFileVersionRegex();

        [GeneratedRegex(CsharpAssemblyAttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
        public static partial Regex CsharpAssemblyAttributeRegex();

        [GeneratedRegex(FsharpAssemblyAttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
        public static partial Regex FsharpAssemblyAttributeRegex();

        [GeneratedRegex(VisualBasicAssemblyAttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Multiline)]
        public static partial Regex VisualBasicAssemblyAttributeRegex();
    }

    internal static partial class VersionCalculation
    {
        public const string DefaultMajorRegexPattern = @"\+semver:\s?(breaking|major)";
        public const string DefaultMinorRegexPattern = @"\+semver:\s?(feature|minor)";
        public const string DefaultPatchRegexPattern = @"\+semver:\s?(fix|patch)";
        public const string DefaultNoBumpRegexPattern = @"\+semver:\s?(none|skip)";

        [GeneratedRegex(DefaultMajorRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultMajorRegex();

        [GeneratedRegex(DefaultMinorRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultMinorRegex();

        [GeneratedRegex(DefaultPatchRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultPatchRegex();

        [GeneratedRegex(DefaultNoBumpRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex DefaultNoBumpRegex();
    }

    internal static partial class SemanticVersion
    {
        public const string ParseStrictRegexPattern =
            @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)"
            + @"(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?"
            + @"(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$";

        public const string ParseLooseRegexPattern =
            @"^(?<SemVer>(?<Major>\d+)(\.(?<Minor>\d+))?(\.(?<Patch>\d+))?)"
            + @"(\.(?<FourthPart>\d+))?(-(?<Tag>[^\+]*))?(\+(?<BuildMetaData>.*))?$";

        public const string ParseBuildMetaDataRegexPattern =
            @"(?<BuildNumber>\d+)?(\.?Branch(Name)?\.(?<BranchName>[^\.]+))?"
            + @"(\.?Sha?\.(?<Sha>[^\.]+))?(?<Other>.*)";

        public const string FormatBuildMetaDataRegexPattern = "[^0-9A-Za-z-.]";

        public const string ParsePreReleaseTagRegexPattern = @"(?<name>.*?)\.?(?<number>\d+)?$";

        [GeneratedRegex(ParseStrictRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ParseStrictRegex();

        [GeneratedRegex(ParseLooseRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ParseLooseRegex();

        [GeneratedRegex(ParseBuildMetaDataRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ParseBuildMetaDataRegex();

        [GeneratedRegex(FormatBuildMetaDataRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex FormatBuildMetaDataRegex();

        [GeneratedRegex(ParsePreReleaseTagRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        public static partial Regex ParsePreReleaseTagRegex();
    }

    internal static partial class AssemblyVersion
    {
        internal static partial class CSharp
        {
            public const string TriviaRegexPattern = "/\\*(.*?)\\*/|//(.*?)\\r?\\n|\\\"((\\\\[^\\n]|[^\\\"\\n])*)\\\"";

            public const string AttributeRegexPattern =
                @"(?x)\[\s*assembly\s*:\s*(System\s*\.\s*Reflection\s*\.\s*)?Assembly(File|Informational)?Version\s*\(\s*\)\s*\]";

            [GeneratedRegex(TriviaRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
            public static partial Regex TriviaRegex();

            [GeneratedRegex(AttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace)]
            public static partial Regex AttributeRegex();
        }

        internal static partial class FSharp
        {
            // TriviaRegexPattern is identical to the C# one so reuse it.

            public const string AttributeRegexPattern =
                @"(?x)\[\s*<\s*assembly\s*:\s*(System\s*\.\s*Reflection\s*\.\s*)?Assembly(File|Informational)?Version\s*\(\s*\)\s*>\s*\]";

            [GeneratedRegex(AttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace)]
            public static partial Regex AttributeRegex();
        }

        internal static partial class VisualBasic
        {
            public const string TriviaRegexPattern = "'(.*?)\\r?\\n|\\\"((\\\\[^\\n]|[^\\\"\\n])*)\\\"";

            public const string AttributeRegexPattern =
                @"(?x)<\s*Assembly\s*:\s*(System\s*\.\s*Reflection\s*\.\s*)?Assembly(File|Informational)?Version\s*\(\s*\)\s*>";

            [GeneratedRegex(TriviaRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
            public static partial Regex TriviaRegex();

            [GeneratedRegex(AttributeRegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace)]
            public static partial Regex AttributeRegex();
        }
    }
}
