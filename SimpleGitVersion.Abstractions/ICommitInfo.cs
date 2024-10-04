using CSemVer;
using System.Collections.Generic;

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
        /// Gets whether the parent graph of the starting commit has not been fully analyzed because we
        /// are on a shallow cloned repository.
        /// </summary>
        public bool IsShallowCloned { get; }

        /// <summary>
        /// Gets this commit and its tag if it's tagged with a version.
        /// Null if there is no release tag on the current commit (or an <see cref="Error"/> occurred).
        /// <para>
        /// If there's no error and this is not null then this version necessarily:
        /// <list type="bullet">
        ///     <item>appears in the <see cref="PossibleVersions"/>;</item>
        ///     <item>is greater than the <see cref="BestCommitBelow"/> or <see cref="AlreadyExistingVersion"/>;</item>
        /// </list>
        /// </para>
        /// <para>
        /// You can always use the <see cref="FinalBuildInfo"/> that holds the <see cref="ICommitBuildInfo.CommitSha"/>
        /// and <see cref="ICommitBuildInfo.CommitDateUtc"/> of this starting commit if needed.
        /// </para>
        /// </summary>
        ITagCommit? ThisReleaseTag { get; }

        /// <summary>
        /// Gets the previous version, that is the best version associated to a commit
        /// below this commit.
        /// This is null if no previous version has been found.
        /// </summary>
        ITagCommit? BestCommitBelow { get; }

        /// <summary>
        /// Gets the commit with the version for that exact same content if it exists.
        /// This is independent of <see cref="ThisReleaseTag"/> (except that, if both exist, they are
        /// necessarily different).
        /// <para>
        /// When there is an already tag for this commit's content, release should usually be skipped.
        /// However exceptions exists and this can be ignored (SimpleGitVersionCore RepositoryInfoOptions has a
        /// IgnoreAlreadyExistingVersion boolean optional flag that allows <see cref="PossibleVersions"/> to be
        /// computed even if a commit with the same content has a release tag).
        /// </para>
        /// </summary>
        ITagCommit? AlreadyExistingVersion { get; }

        /// <summary>
        /// Gets CI informations if a CI release can be done: <see cref="ThisReleaseTag"/> is necessarily null.
        /// </summary>
        ICIReleaseInfo? CIRelease { get; }

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

        /// <summary>
        /// Gets the <see cref="IRepositoryInfo"/> of this commit.
        /// </summary>
        IRepositoryInfo RepositoryInfo { get; }
    }
}
