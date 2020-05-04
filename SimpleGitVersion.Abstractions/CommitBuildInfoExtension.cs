using CSemVer;

namespace SimpleGitVersion
{
    /// <summary>
    /// Extends <see cref="ICommitBuildInfo"/>.
    /// </summary>
    public static class CommitBuildInfoExtension
    {
        /// <summary>
        /// Gets whether the <see cref="ICommitBuildInfo.Version"/> is not the <see cref="SVersion.ZeroVersion"/>.
        /// </summary>
        /// <param name="this">This build info.</param>
        /// <returns>True if this version is not the zero version.</returns>
        public static bool IsValid( this ICommitBuildInfo @this ) => !@this.Version.IsZeroVersion;
    }

}
