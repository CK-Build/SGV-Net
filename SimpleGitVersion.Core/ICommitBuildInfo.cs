using CSemVer;
using System;

namespace SimpleGitVersion
{
    /// <summary>
    /// Exposes all information version required to build a project.
    /// Implementation should default to Zero Version if anything prevents its computation.
    /// </summary>
    public interface ICommitBuildInfo
    {
        /// <summary>
        /// Gets "Debug" for ci build or prerelease below "rc" and "Release" for "rc" and official releases.
        /// Never null, defaults to "Debug".
        /// </summary>
        string BuildConfiguration { get; }

        /// <summary>
        /// Gets the Sha of the commit.
        /// Defaults to <see cref="InformationalVersion.ZeroCommitSha"/>.
        /// </summary>
        string CommitSha { get; }

        /// <summary>
        /// Gets the UTC date and time of the commit.
        /// Defaults to <see cref="InformationalVersion.ZeroCommitDate"/>.
        /// </summary>
        DateTime CommitDateUtc { get; }

        /// <summary>
        /// Gets the standardized information version string that must be used to build this
        /// commit point.
        /// Never null: may be the <see cref="SVersion.ZeroVersion"/> with this <see cref="CommitSha"/> and <see cref="CommitDateUtc"/>
        /// and ultimately defaults to <see cref="InformationalVersion.ZeroInformationalVersion"/>.
        /// string.
        /// </summary>
        string InformationalVersion { get; }

        /// <summary>
        /// Gets the normalized version (short form) that must be used to build this commit point.
        /// Never null: defaults to <see cref="SVersion.ZeroVersion"/>.
        /// </summary>
        SVersion Version { get; }

        /// <summary>
        /// Gets the "Major.Minor" string.
        /// Never null, defaults to "0.0".
        /// </summary>
        string AssemblyVersion { get; }

        /// <summary>
        /// Gets the 'Major.Minor.Build.Revision' windows file version to use based on the <see cref="CSVersion.OrderedVersion"/>.
        /// When it is a release the last part (Revision) is even and it is odd for CI builds (except when <see cref="CIBranchVersionMode.ZeroTimed"/> is used). 
        /// Defaults to '0.0.0.0' (<see cref="InformationalVersion.ZeroFileVersion"/>).
        /// See <see cref="CSVersion.ToStringFileVersion(bool)"/>.
        /// </summary>
        string FileVersion { get; }
    }

}
