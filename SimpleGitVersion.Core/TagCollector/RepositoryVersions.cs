using CSemVer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleGitVersion
{
    class RepositoryVersions
    {
        readonly IReadOnlyList<TagCommit> _versions;

        internal RepositoryVersions( IEnumerable<TagCommit> collected, StringBuilder errors )
        {
            Debug.Assert( collected.All( c => c.ThisTag != null ) );
            _versions = collected.OrderBy( t => t.ThisTag ).ToList();
        }

        public RepositoryInfo.ErrorCodeStatus CheckExistingVersions( StringBuilder errors, CSVersion? startingVersion )
        {
            if( _versions.Count > 0 )
            {
                var first = _versions[0].ThisTag;
                if( startingVersion == null )
                {
                    if( !CSVersion.FirstPossibleVersions.Contains( first ) )
                    {
                        errors.AppendFormat( $"First existing version is '{first}' (on '{_versions[0].CommitSha}'). A very first version is missing ({String.Join( ", ", CSVersion.FirstPossibleVersions.Select( v => v.ToString() ) )}) or a StartingVersion must be specified.." )
                                .AppendLine();
                        return RepositoryInfo.ErrorCodeStatus.CheckExistingVersionFirstMissing;
                    }
                }
                bool foundStartingVersion = false;
                bool atLeastOneHole = false;
                for( int i = 0; i < _versions.Count - 1; ++i )
                {
                    var prev = _versions[i].ThisTag;
                    foundStartingVersion |= prev == startingVersion;
                    var next = _versions[i + 1].ThisTag;
                    Debug.Assert( next != prev, "Unicity has been already been handled." );
                    if( !next.IsDirectPredecessor( prev ) )
                    {
                        errors.AppendFormat( $"Missing one or more version(s) between '{prev}' and '{next}'." )
                                .AppendLine();
                        atLeastOneHole = true;
                    }
                }
                if( !foundStartingVersion && startingVersion != null ) return RepositoryInfo.ErrorCodeStatus.CheckExistingVersionStartingVersionNotFound;
                if( atLeastOneHole ) return RepositoryInfo.ErrorCodeStatus.CheckExistingVersionHoleFound;
            }
            return RepositoryInfo.ErrorCodeStatus.None;
        }

        internal IReadOnlyList<TagCommit> TagCommits => _versions;

        public IReadOnlyList<IFullTagCommit> Versions => _versions;

    }
}
