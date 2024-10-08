using CSemVer;

namespace SimpleGitVersion
{

    /// <summary>
    /// Describes a commit in the repository with its associated <see cref="CSVersion"/>.
    /// </summary>
    public interface ITagCommit
    {
        /// <summary>
        /// Gets this commit Sha.
        /// </summary>
        string CommitSha { get; }

        /// <summary>
        /// Gets the valid tag directly associated to this <see cref="CommitSha"/>.
        /// </summary>
        CSVersion ThisTag { get; }

    }

}
