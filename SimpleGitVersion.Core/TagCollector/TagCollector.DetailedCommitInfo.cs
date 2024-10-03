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
                _default ??= new FilteredView( this, null );
                return _default;
            }
            if( _filtered == null ) _filtered = new Dictionary<CSVersion, FilteredView>();
            else if( _filtered.TryGetValue( excluded, out var view ) ) return view;
            var v = new FilteredView( this, excluded );
            _filtered.Add( excluded, v );
            return v;
        }

        public DetailedCommitInfo GetCommitInfo( Commit c )
        {
            Debug.Assert( ExistingVersions != null, "No error." );
            var (basic,shallow) = GetCommitView( null ).GetInfo( c );

            // Default to null.
            ITagCommit? alreadyExistingVersion = null;
            ITagCommit? bestCommitBelow = null;

            IReadOnlyList<CSVersion> nextPossibleVersions;
            IReadOnlyList<CSVersion> possibleVersions;
            // Special case: there is no existing versions but there is a startingVersion, every commit may be
            // this starting version (and alreadyExistingVersion and bestCommitBelow are obviously null). 
            if( _startingVersion != null && ExistingVersions.Versions.Count == 0 )
            {
                possibleVersions = nextPossibleVersions = new[] { _startingVersion };
            }
            else
            {
                if( basic == null )
                {
                    // No information at all on this commit (means it has no versions on or below, even based on its content).
                    // We compute the next possible versions based on null: the first CSemVer versions will be considered
                    // and this is also the PossibleVersions.
                    // The alreadyExistingVersion and bestCommitBelow remain null.
                    possibleVersions = nextPossibleVersions = GetPossibleVersions( null, null );
                }
                else
                {
                    // Some information exist: the MaxCommit exists necessarily. We can compute the next versions based on it.
                    nextPossibleVersions = GetPossibleVersions( basic.MaxCommit.ThisTag, null );
                    // If there is no tag on the commit itself, then:
                    // - The PossibleVersions are the same as the next ones.
                    // - The alreadyExistingVersion and bestCommitBelow come from this commit.
                    if( basic.UnfilteredThisCommit == null )
                    {
                        possibleVersions = nextPossibleVersions;
                        alreadyExistingVersion = basic.BestCommit;
                        bestCommitBelow = basic.BestCommitBelow;
                    }
                    else
                    {
                        // Special case: there is no existing versions (other than this tag on the commit itself) but there is a
                        // StartingVersion, every commit may be the first one (and alreadyExistingVersion and bestCommitBelow remain null). 
                        if( _startingVersion != null && ExistingVersions.Versions.Count == 1 )
                        {
                            possibleVersions = new[] { _startingVersion };
                        }
                        else
                        {
                            // Since this commit is tagged, to compute the PossibleVersions, we must do as if this tag doesn't exist at all.
                            // This is where we need the "View" that excludes this version.
                            var excluded = basic.UnfilteredThisCommit.ThisTag;
                            BasicCommitInfo? noThisVersion = GetCommitView( excluded ).GetInfo( c ).Info;
                            // Since we excluded this tag, there may be... nothing: noThisVersion can be null.
                            // The BestCommit of this noThisVersion is our high level AlreadyExistingVersion and
                            // its BestCommitBelow is also our high level BestCommitBelow.
                            alreadyExistingVersion = noThisVersion?.BestCommit;
                            bestCommitBelow = noThisVersion?.BestCommitBelow;
                            // The possible versions for this commit is computed based on noThisVersion's MaxCommit.
                            // Final ThisReleaseTag (that is basic.UnfilteredThisCommit.ThisTag) will be tested
                            // against this set of versions and if it doesn't appear in them, the ReleaseTagIsNotPossible
                            // error is set: when the CommitInfo has no error and ThisReleaseTag is not null then it's
                            // version is necessarily greater than the ones of the AlreadyExisitingVersion or BestCommitBelow.
                            possibleVersions = GetPossibleVersions( noThisVersion?.MaxCommit.ThisTag, excluded );
                        }
                    }

                }
            }
            return new DetailedCommitInfo( c.Sha, basic, shallow, alreadyExistingVersion, bestCommitBelow, possibleVersions, nextPossibleVersions );
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
            return successors.Where( v => v > _startingVersion
                                              && (nextReleased == null || v < nextReleased.ThisTag) )
                             .ToList();
        }


    }
}
