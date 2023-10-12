namespace SimpleGitVersion
{
    /// <summary>
    /// Minimal repository information.
    /// </summary>
    public interface IRepositoryInfo
    {
        /// <summary>
        /// Gets the remote url "origin" if found.
        /// </summary>
        public string? RemoteUrl { get; }

        /// <summary>
        /// Gets the working folder (where /.git sub folder is)
        /// if it has been found and is valid.
        /// </summary>
        public string? WorkingDirectory { get; }
    }
}
