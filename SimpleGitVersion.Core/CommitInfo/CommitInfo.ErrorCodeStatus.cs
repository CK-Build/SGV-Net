using CSemVer;

namespace SimpleGitVersion
{

    public partial class CommitInfo
    {
        /// <summary>
        /// Error code of the <see cref="Error"/>.
        /// This is local to this <see cref="CommitInfo"/> and should not be exposed on the <see cref="ICommitInfo"/> abstraction
        /// since this captures implementation related aspects.
        /// </summary>
        public enum ErrorCodeStatus
        {
            /// <summary>
            /// No error: <see cref="Error"/> is null.
            /// </summary>
            None,

            /// <summary>
            /// Git repository missing.
            /// </summary>
            InitNoGitRepository,

            /// <summary>
            /// Unitialized Git Repository: no repository's head exists.
            /// </summary>
            InitUnitializedGitRepository,

            /// <summary>
            /// The <see cref="RepositoryInfoOptions.HeadBranchName"/> has not been found
            /// either as a local or as a remote (on <see cref="RepositoryInfoOptions.RemoteName"/> origin) branch.
            /// </summary>
            InitHeadBranchNameNotFound,

            /// <summary>
            /// The remote <see cref="RepositoryInfoOptions.HeadBranchName"/> has not been found.
            /// </summary>
            InitHeadRemoteBranchNameNotFound,

            /// <summary>
            /// The <see cref="RepositoryInfoOptions.HeadCommit"/> has not been found.
            /// </summary>
            InitHeadCommitNotFound,

            /// <summary>
            /// The working folder is dirty and <see cref="RepositoryInfoOptions.IgnoreDirtyWorkingFolder"/> is false.
            /// </summary>
            DirtyWorkingFolder,

            /// <summary>
            /// The <see cref="RepositoryInfoOptions.StartingVersion"/> is invalid.
            /// </summary>
            InvalidStartingVersion,

            /// <summary>
            /// The <see cref="RepositoryInfoOptions.StartingVersion"/> conflicts with <see cref="RepositoryInfoOptions.SingleMajor"/>.
            /// </summary>
            StartingVersionConflictsWithSingleMajor,

            /// <summary>
            /// One of the <see cref="RepositoryInfoOptions.OverriddenTags"/> cannot be resolved.
            /// </summary>
            InvalidOverriddenTag,

            /// <summary>
            /// At least one commit has multiple version tags. +invalid should be used.
            /// </summary>
            MultipleVersionTagConflict,

            /// <summary>
            /// When <see cref="RepositoryInfoOptions.CheckExistingVersions"/> is true and no <see cref="RepositoryInfoOptions.StartingVersion"/>
            /// has been specified: one of the existing version must belong to <see cref="CSVersion.FirstPossibleVersions"/>.
            /// </summary>
            CheckExistingVersionFirstMissing,

            /// <summary>
            /// When <see cref="RepositoryInfoOptions.CheckExistingVersions"/> is true: the <see cref="RepositoryInfoOptions.StartingVersion"/> commit was not found.
            /// </summary>
            CheckExistingVersionStartingVersionNotFound,

            /// <summary>
            /// When <see cref="RepositoryInfoOptions.CheckExistingVersions"/> is true: existing versions are not compact.
            /// </summary>
            CheckExistingVersionHoleFound,

            /// <summary>
            /// The existing version tag conflicts with <see cref="RepositoryInfoOptions.SingleMajor"/> or <see cref="RepositoryInfoOptions.OnlyPatch"/>.
            /// </summary>
            ReleaseTagConflictsWithSingleMajorOrOnlyPatch,

            /// <summary>
            /// The existing version tag does not belong to the set of <see cref="CommitInfo.PossibleVersions"/>.
            /// </summary>
            ReleaseTagIsNotPossible,

            /// <summary>
            /// The <see cref="RepositoryInfoOptionsBranch.Name"/> is too long for CI build.
            /// </summary>
            CIBuildVersionNameTooLong,

            /// <summary>
            /// The specified <see cref="RepositoryInfoOptions.HeadCommit"/> has been specified is not the tip of any branch.
            /// Since no branches reference this commit we cannot find a <see cref="RepositoryInfoOptionsBranch"/>
            /// to compute a CI version.
            /// </summary>
            CIBuildHeadCommitIsDetached,

            /// <summary>
            /// The current repository's head is not the tip of any branch. Since no branches reference the current repository's head commit
            /// we cannot find a <see cref="RepositoryInfoOptionsBranch"/> to compute a CI version.
            /// </summary>
            CIBuildRepositoryHeadIsDetached,

            /// <summary>
            /// There is no CI Branch information defined for one of the branch that reference the current commit.
            /// </summary>
            CIBuildMissingBranchOption,

            /// <summary>
            /// The <see cref="RepositoryInfoOptionsBranch.CIVersionMode"/> is <see cref="CIBranchVersionMode.None"/>.
            /// </summary>
            CIBuildBranchExplicitNone,

            /// <summary>
            /// A commit with the exact same content exists with a version tag and <see cref="RepositoryInfoOptions.IgnoreAlreadyExistingVersion"/> is false.
            /// </summary>
            AlreadyExistingVersion,

            /// <summary>
            /// Temporary: RepositoryInfo.xml file needs an upgrade.
            /// </summary>
            OptionsXmlMigrationRequired
        }
    }
}
