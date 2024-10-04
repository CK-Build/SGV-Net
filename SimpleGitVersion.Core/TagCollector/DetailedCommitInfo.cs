using CSemVer;
using System.Collections.Generic;
using System.Diagnostics;

namespace SimpleGitVersion;

/// <summary>
/// Commit info that exposes raw <see cref="PossibleVersions"/> and <see cref="NextPossibleVersions"/>
/// (<see cref="RepositoryInfoOptions.SingleMajor"/> and <see cref="RepositoryInfoOptions.OnlyPatch"/>
/// are ignored at this level).
/// </summary>
public class DetailedCommitInfo
{
    /// <summary>
    /// Gets this commit sha.
    /// </summary>
    public readonly string CommitSha;

    /// <summary>
    /// The basic commit info.
    /// Can be null if no version information can be found in the repository on or
    /// below this commit point.
    /// </summary>
    public readonly BasicCommitInfo? BasicInfo;

    /// <summary>
    /// Whether the parent graph has not been fully analyzed because we are on a shallow cloned repository.
    /// </summary>
    public readonly bool IsShallowCloned;

    /// <summary>
    /// Gets the commit with the version for that exact same content if it exists.
    /// </summary>
    public readonly ITagCommit? AlreadyExistingVersion;

    /// <summary>
    /// Gets the previous version, that is the best version associated to a commit
    /// below this commit.
    /// </summary>
    public readonly ITagCommit? BestCommitBelow;

    /// <summary>
    /// Gets the possible next versions based on this commit.
    /// </summary>
    public readonly IReadOnlyList<CSVersion> NextPossibleVersions;

    /// <summary>
    /// Gets the possible versions on this commit regardless of the tag already set on it.
    /// </summary>
    public readonly IReadOnlyList<CSVersion> PossibleVersions;


    internal DetailedCommitInfo( string sha,
                                 BasicCommitInfo? basic,
                                 bool isShallowCloned,
                                 ITagCommit? alreadyExistingVersion,
                                 ITagCommit? bestCommitBelow,
                                 IReadOnlyList<CSVersion> possibleVersions,
                                 IReadOnlyList<CSVersion> nextPossibleVersions )
    {
        CommitSha = sha;
        BasicInfo = basic;
        IsShallowCloned = isShallowCloned;
        AlreadyExistingVersion = alreadyExistingVersion;
        BestCommitBelow = bestCommitBelow;
        NextPossibleVersions = nextPossibleVersions;
        PossibleVersions = possibleVersions;

        Debug.Assert( AlreadyExistingVersion == null
                      || BasicInfo?.UnfilteredThisCommit == null
                      || AlreadyExistingVersion.ThisTag != BasicInfo?.UnfilteredThisCommit.ThisTag,
                      "AlreadyExistingVersion is independent of the release tag, except that, if both exist, they are necessarily different." );
    }
}
