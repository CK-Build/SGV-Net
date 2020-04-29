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
            SVersion ciBaseTag,
            int ciBaseDepth,
            SVersion ciBuildVersion,
            bool isZeroTimed )
        {
            Debug.Assert( ciBaseTag.AsCSVersion != null || (ciBaseTag == SVersion.ZeroVersion && isZeroTimed), "If the base tag is a SVersion, it is the ZeroVersion AND we use the ZeroTimed mode." );
            BaseTag = ciBaseTag;
            Depth = ciBaseDepth;
            BuildVersion = ciBuildVersion;
            IsZeroTimed = isZeroTimed;
        }

        /// <summary>
        /// The base <see cref="CSVersion"/> from which <see cref="BuildVersion"/> is built,
        /// or the <see cref="SVersion.ZeroVersion"/> if no previous tag has been found from the commit.
        /// When no previous tag has been found (ZeroVersion), then <see cref="IsZeroTimed"/> is necessarily true.
        /// </summary>
        public readonly SVersion BaseTag;

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

        internal static CIReleaseInfo Create( Commit commit, CIBranchVersionMode ciVersionMode, string ciBuildName, BasicCommitInfo? info )
        {
            Debug.Assert( ciBuildName.Length <= 8 );
            var actualBaseTag = info?.MaxCommit.ThisTag;
            SVersion? ciBuildVersion = null;

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
                return new CIReleaseInfo( SVersion.ZeroVersion, 0, ciBuildVersion, true );

            }
            Debug.Assert( ciVersionMode == CIBranchVersionMode.LastReleaseBased );
            Debug.Assert( info != null, "Since actualBaseTag is not null." );
            CIBuildDescriptor ci = new CIBuildDescriptor { BranchName = ciBuildName, BuildIndex = info!.BelowDepth };
            ciBuildVersion = SVersion.Parse( actualBaseTag.ToString( CSVersionFormat.Normalized, ci ), false );
            return new CIReleaseInfo( actualBaseTag, info.BelowDepth, ciBuildVersion, isZeroTimed: false );
        }
    }

}

