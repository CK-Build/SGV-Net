using CSemVer;
using System;

namespace SimpleGitVersion
{
    /// <summary>
    /// Encapsulates CI release information: must be available if and only if a CI build can be done.
    /// </summary>
    public interface ICIReleaseInfo
    {
        /// <summary>
        /// The base <see cref="CSVersion"/> from which <see cref="BuildVersion"/> is built,
        /// or the <see cref="SVersion.ZeroVersion"/> if no previous tag has been found from the commit.
        /// When no previous tag has been found (ZeroVersion), then <see cref="IsZeroTimed"/> is necessarily true.
        /// </summary>
        SVersion BaseTag { get; }

        /// <summary>
        /// The greatest number of commits between the current commit and the deepest occurrence 
        /// of <see cref="BaseTag"/>.
        /// </summary>
        int Depth { get; }

        /// <summary>
        /// Never null: this is the CSemVer-CI version in <see cref="CSVersionFormat.Normalized"/> format.
        /// </summary>
        SVersion BuildVersion { get; }

        /// <summary>
        /// Gets whether this version is a Zero timed based: the version will be zero based like
        /// <c>0.0.0--ci.SortableUtcDateTime.BranchName</c> (long form) or <c>0.0.0--NumberOfSecondsFrom20150101-BranchName</c>
        /// (short form).
        /// <see cref="CIBuildDescriptor.CreateLongFormZeroTimed(string, DateTime)"/> and <see cref="CIBuildDescriptor.CreateShortFormZeroTimed(string, DateTime)"/>.
        /// </summary>
        bool IsZeroTimed { get; }
    }

}

