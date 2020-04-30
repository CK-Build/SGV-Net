using CSemVer;

namespace SimpleGitVersion
{
    /// <summary>
    /// Extends <see cref="ICommitBuildInfo"/>.
    /// </summary>
    public static class CommitBuildInfoExtension
    {
        /// <summary>
        /// Gets whether the <see cref="ICommitBuildInfo.CommitSha"/> is the <see cref="InformationalVersion.ZeroCommitSha"/>.
        /// </summary>
        /// <param name="this">This build info.</param>
        /// <returns>True if this is the zero commit sha.</returns>
        public static bool IsZeroCommit( this ICommitBuildInfo @this ) => @this.CommitSha == InformationalVersion.ZeroCommitSha;
    }

}
