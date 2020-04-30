using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSemVer;
using LibGit2Sharp;

namespace SimpleGitVersion
{

    partial class TagCollector
    {
        FilteredView? _default;
        Dictionary<CSVersion, FilteredView>? _filtered;

        FilteredView GetCommitView( CSVersion? excluded )
        {
            Debug.Assert( ExistingVersions != null, "No error." );
            Debug.Assert( excluded == null || ExistingVersions.Versions.Any( t => t.ThisTag == excluded ) );
            if( excluded == null )
            {
                if( _default == null ) _default = new FilteredView( this, null );
                return _default;
            }
            if( _filtered == null ) _filtered = new Dictionary<CSVersion, FilteredView>();
            else if( _filtered.TryGetValue( excluded, out var view ) ) return view;
            var v = new FilteredView( this, excluded );
            _filtered.Add( excluded, v );
            return v;
        }

        public CommitInfo GetCommitInfo( Commit c )
        {
            Debug.Assert( ExistingVersions != null, "No error." );
            BasicCommitInfo? basic = GetCommitView( null ).GetInfo( c );

            IReadOnlyList<CSVersion> nextPossibleVersions;
            IReadOnlyList<CSVersion> possibleVersions;
            // Special case: there is no existing versions but there is a startingVersionForCSemVer,
            // every commit may be this starting version. 
            if( _startingVersionForCSemVer != null && ExistingVersions.Versions.Count == 0 )
            {
                possibleVersions = nextPossibleVersions = new[] { _startingVersionForCSemVer };
            }
            else
            {
                nextPossibleVersions = GetPossibleVersions( basic?.MaxCommit.ThisTag, null );
                bool thisHasCommit = basic?.UnfilteredThisCommit != null;
                // Special case: there is no existing versions (other than this one that is skipped if it exists) but
                // there is a startingVersionForCSemVer, every commit may be the first one. 
                if( _startingVersionForCSemVer != null
                    && ExistingVersions.Versions.Count == 1
                    && thisHasCommit )
                {
                    possibleVersions = new[] { _startingVersionForCSemVer };
                }
                else
                {
                    if( thisHasCommit )
                    {
                        var excluded = basic!.UnfilteredThisCommit!.ThisTag;
                        var noVersion = GetCommitView( excluded ).GetInfo( c );
                        possibleVersions = GetPossibleVersions( noVersion?.MaxCommit.ThisTag, excluded );
                    }
                    else possibleVersions = nextPossibleVersions;
                }
            }
            return new CommitInfo( c.Sha, basic, possibleVersions, nextPossibleVersions );
        }

        List<CSVersion> GetPossibleVersions( CSVersion? baseVersion, CSVersion? excluded )
        {
            Debug.Assert( ExistingVersions != null, "No error." );
            // The base version can be null here: a null version tag correctly generates 
            // the very first possible versions (and the comparison operators handle null).
            IEnumerable<IFullTagCommit> allVersions = ExistingVersions.Versions;
            if( excluded != null ) allVersions = allVersions.Where( c => c.ThisTag != excluded ); 
            var nextReleased = allVersions.FirstOrDefault( c => c.ThisTag > baseVersion );
            var successors = CSVersion.GetDirectSuccessors( false, baseVersion );
            return successors.Where( v => v > _startingVersionForCSemVer
                                              && (nextReleased == null || v < nextReleased.ThisTag) )
                             .ToList();
        }


    }
}
