using CSemVer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SimpleGitVersion;

sealed class RepositoryVersions
{
    readonly IReadOnlyList<TagCommit> _versions;
    readonly TagCommit? _startingVersionCommit;

    internal RepositoryVersions( IEnumerable<TagCommit> collected, TagCommit? startingVersionCommit )
    {
        Debug.Assert( collected.All( c => c.ThisTag != null ) );
        _versions = collected.OrderBy( t => t.ThisTag ).ToList();
        _startingVersionCommit = startingVersionCommit;
    }

    public CommitInfo.ErrorCodeStatus CheckExistingVersions( StringBuilder errors, CSVersion? startingVersion )
    {
        if( _versions.Count > 0 )
        {
            var first = _versions[0].ThisTag;
            if( startingVersion == null )
            {
                if( !CSVersion.FirstPossibleVersions.Contains( first ) )
                {
                    errors.AppendFormat( $"First existing version is '{first}' (on '{_versions[0].CommitSha}'). A very first version is missing ({String.Join( ", ", CSVersion.FirstPossibleVersions.Select( v => v.ToString() ) )}) or a StartingVersion must be specified." )
                            .AppendLine();
                    return CommitInfo.ErrorCodeStatus.CheckExistingVersionFirstMissing;
                }
            }
            bool atLeastOneHole = false;
            for( int i = 0; i < _versions.Count - 1; ++i )
            {
                var prev = _versions[i].ThisTag;
                var next = _versions[i + 1].ThisTag;
                Debug.Assert( next != prev, "Unicity has already been handled." );
                if( !next.IsDirectPredecessor( prev ) )
                {
                    errors.AppendFormat( $"Missing one or more version(s) between '{prev}' and '{next}'." )
                            .AppendLine();
                    atLeastOneHole = true;
                }
            }
            if( startingVersion != null && _startingVersionCommit == null )
            {
                errors.AppendLine( $"Missing specified StartingVersion='{startingVersion}'." );
                return CommitInfo.ErrorCodeStatus.CheckExistingVersionStartingVersionNotFound;
            }
            if( atLeastOneHole ) return CommitInfo.ErrorCodeStatus.CheckExistingVersionHoleFound;
        }
        return CommitInfo.ErrorCodeStatus.None;
    }

    internal IReadOnlyList<TagCommit> TagCommits => _versions;

    internal TagCommit? StartingVersionCommit => _startingVersionCommit;
}
