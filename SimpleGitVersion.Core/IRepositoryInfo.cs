using CSemVer;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleGitVersion
{
    /// <summary>
    /// Minimal information we need to know in terms of versions from a given commit in a Git repository to reason
    /// on new versions or to generate a build thanks to <see cref="FinalBuildInfo"/>.
    /// <para>
    /// The <see cref="FinalBuildInfo"/> holds the <see cref="ICommitBuildInfo.CommitSha"/> and <see cref="ICommitBuildInfo.CommitDateUtc"/>
    /// for this analyzed commit along with every information needed to produce a correctly stamped artifact (even if it is
    /// the <see cref="SVersion.ZeroVersion"/> if an <see cref="Error"/> occurred).
    /// </para>
    /// </summary>
    public interface IRepositoryInfo
    {
        /// <summary>
        /// Gets either <see cref="RepositoryInfo.StartingCommitInfo.Error"/> or <see cref="RepositoryInfo.CommitAnalyzingError"/>.
        /// Null if no error occurred.
        /// </summary>
        string? Error { get; }

        /// <summary>
        /// Gets the version directly associated to this commit.
        /// This is null if there is actually no release tag on the current commit.
        /// </summary>
        CSVersion? ValidReleaseTag { get; }

        /// <summary>
        /// Gets CI informations if a CI release can be done: <see cref="ValidReleaseTag"/> is necessarily null.
        /// Not null only if we are on a branch that is enabled in <see cref="RepositoryInfoOptions.Branches"/> (either 
        /// because it is the current branch or <see cref="RepositoryInfoOptions.StartingBranchName"/> specifies it),
        /// and the <see cref="RepositoryInfoOptions.StartingCommitSha"/> is null or empty.
        /// </summary>
        CIReleaseInfo? CIRelease { get; }

        /// <summary>
        /// Gets the commit with a better version for that exact same content if it exists.
        /// This is independent of <see cref="ValidReleaseTag"/>.
        /// </summary>
        ITagCommit? BetterExistingVersion { get; }

        /// <summary>
        /// Gets the previous version, that is the best version associated to a commit
        /// below this commit.
        /// This is null if no previous version has been found.
        /// </summary>
        ITagCommit? BestCommitBelow { get; }

        /// <summary>
        /// Gets the possible versions for the current commit point.
        /// When empty, this means that there cannot be a valid release tag on this commit.
        /// Null if an <see cref="Error"/> prevented its computation.
        /// </summary>
        IReadOnlyList<CSVersion>? PossibleVersions { get; }

        /// <summary>
        /// Gets the versions that may be available to any commit above this commit.
        /// Null if an <see cref="Error"/> prevented its computation.
        /// </summary>
        IReadOnlyList<CSVersion>? NextPossibleVersions { get; }

        /// <summary>
        /// Gets the <see cref="ICommitBuildInfo"/> for this commit.
        /// This holds the <see cref="ICommitBuildInfo.CommitSha"/> and <see cref="ICommitBuildInfo.CommitDateUtc"/> of this commit.
        /// </summary>
        ICommitBuildInfo FinalBuildInfo { get; }
    }
}
