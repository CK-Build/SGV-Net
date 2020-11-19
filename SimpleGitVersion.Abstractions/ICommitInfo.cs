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
    public interface ICommitInfo
    {
        /// <summary>
        /// Gets any error that may have prevented a <see cref="CommitBuildInfoExtension.IsValid(ICommitBuildInfo)">valid</see>
        /// <see cref="FinalBuildInfo"/> to be obtained.
        /// </summary>
        string? Error { get; }

        /// <summary>
        /// Gets whether the parent graph of the starting commit has not been fully anlayzed because we
        /// are on a shallow cloned repositry.
        /// </summary>
        public bool IsShallowCloned { get; }

        /// <summary>
        /// Gets the version directly associated to this commit.
        /// Null if there is no release tag on the current commit (or an <see cref="Error"/> occured).
        /// </summary>
        CSVersion? ReleaseTag { get; }

        /// <summary>
        /// Gets CI informations if a CI release can be done: <see cref="ReleaseTag"/> is necessarily null.
        /// </summary>
        ICIReleaseInfo? CIRelease { get; }

        /// <summary>
        /// Gets the commit with the version for that exact same content if it exists.
        /// This is independent of <see cref="ReleaseTag"/> (except that, if both exist, they are
        /// necessarily different).
        /// </summary>
        ITagCommit? AlreadyExistingVersion { get; }

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
