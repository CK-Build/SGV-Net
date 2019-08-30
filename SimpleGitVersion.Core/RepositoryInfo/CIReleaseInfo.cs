using CSemVer;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleGitVersion
{
    /// <summary>
    /// Encapsulates CI release information.
    /// Instances of this class are created internally if and only a CI build can 
    /// actually be done.
    /// </summary>
    public class CIReleaseInfo
    {
        CIReleaseInfo(
            CSVersion ciBaseTag,
            int ciBaseDepth,
            SVersion ciBuildVersion,
            bool isZeroTimed )
        {
            BaseTag = ciBaseTag;
            Depth = ciBaseDepth;
            BuildVersion = ciBuildVersion;
            IsZeroTimed = isZeroTimed;
        }

        /// <summary>
        /// The base <see cref="CSVersion"/> from which <see cref="BuildVersion"/> is built.
        /// It is either the previous release or the <see cref="CSVersion.VeryFirstVersion"/>.
        /// </summary>
        public readonly CSVersion BaseTag;

        /// <summary>
        /// The greatest number of commits between the current commit and the deepest occurence 
        /// of <see cref="BaseTag"/>.
        /// </summary>
        public readonly int Depth;

        /// <summary>
        /// Never null: this is the CSemVer-CI version in <see cref="CSVersionFormat.Normalized"/> format.
        /// </summary>
        public readonly SVersion BuildVersion;

        /// <summary>
        /// Gets whether this version is a Zero timed based. See <see cref="CIBranchVersionMode.ZeroTimed"/>,
        /// <see cref="CIBuildDescriptor.CreateLongFormZeroTimed(string, DateTime)"/> and <see cref="CIBuildDescriptor.CreateShortFormZeroTimed(string, DateTime)"/>.
        /// </summary>
        public readonly bool IsZeroTimed;

        internal static CIReleaseInfo Create(
            Commit commit,
            CIBranchVersionMode ciVersionMode,
            string ciBuildName,
            BasicCommitInfo info )
        {
            Debug.Assert( ciBuildName != null && ciBuildName.Length <= 8 );
            var actualBaseTag = info?.MaxCommit.ThisTag;
            CSVersion ciBaseTag = actualBaseTag ?? CSVersion.VeryFirstVersion;
            SVersion ciBuildVersion = null;

            // If there is no base release found, we fall back to ZeroTimedBased mode.
            if( ciVersionMode == CIBranchVersionMode.ZeroTimed || actualBaseTag == null )
            {
                DateTime timeRelease = commit.Committer.When.ToUniversalTime().UtcDateTime;
                string vN = CIBuildDescriptor.CreateShortFormZeroTimed( ciBuildName, timeRelease );
                if( actualBaseTag != null )
                {
                    // The metadata contains the base tag.
                    vN += "+v" + actualBaseTag;
                }
                ciBuildVersion = SVersion.Parse( vN );
                return new CIReleaseInfo( ciBaseTag, 0, ciBuildVersion, true );

            }
            Debug.Assert( ciVersionMode == CIBranchVersionMode.LastReleaseBased && actualBaseTag != null );
            CIBuildDescriptor ci = new CIBuildDescriptor { BranchName = ciBuildName, BuildIndex = info.BelowDepth };
            ciBuildVersion = SVersion.Parse( actualBaseTag.ToString( CSVersionFormat.Normalized, ci ), false );
            return new CIReleaseInfo( ciBaseTag, info.BelowDepth, ciBuildVersion, isZeroTimed: false );
        }
    }

}

