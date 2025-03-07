using CSemVer;
using LibGit2Sharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SimpleGitVersion;


sealed partial class TagCollector
{
    FilteredView? _default;
    Dictionary<CSVersion, FilteredView>? _filtered;

    FilteredView GetCommitView( CSVersion? excluded )
    {
        Debug.Assert( ExistingVersions != null, "No error." );
        Debug.Assert( excluded == null || ExistingVersions.TagCommits.Any( t => t.ThisTag == excluded ) );
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
        var (basic, shallow) = GetCommitView( null ).GetInfo( c );

        // Default to null.
        ITagCommit? alreadyExistingVersion = null;
        ITagCommit? bestCommitBelow = null;

        IReadOnlyList<CSVersion> nextPossibleVersions;
        IReadOnlyList<CSVersion> possibleVersions;

        // If we have a StartingVersion
        //   If a StartingVersion commit exists, either:
        //       - The StartingVersion commit is a parent of this commit (this code "contains"
        //         the StartingVersion): this is a standard case where the StartingVersion commit
        //         (or one of its parent is our base version).
        //       - OR this commit is a parent of the StartingVersion commit. 
        //         This commit cannot be released (PossibleVersions should be empty).
        //         Since any version tag on this commit or above has been ignored, the only
        //         possible versions would be the very first one and since GetPossibleVersions
        //         restricts the versions to be greater or equal to the StartingVersion, the
        //         PossibleVersions will be empty.
        //       - OR this commit and the StartingVersion commit are independent.
        //         If there is a base commit then its version is necessarily greater
        //         than the StartingVersion. Since GetPossibleVersions restricts the versions
        //         to be greater or equal to the StartingVersion, the PossibleVersions will be empty.
        //      => When a StartingVersion commit exists, the standard algorithm is fine.
        //
        // When the configured StartingVersion doesn't exist, we consider that the StartingVersion is
        // a new "starting point", typically in a new branch from an old commit point for which new versions
        // must be produced - typically patch or minor.
        // We allow only the StartingVersion as a PossibleVersion.
        if( _startingVersion != null
            && ExistingVersions.StartingVersionCommit == null )
        {
            possibleVersions = nextPossibleVersions = [_startingVersion];
        }
        else
        {
            if( basic == null )
            {
                // No information at all on this commit (means it has no versions on or below, even based on its content).
                // We compute the next possible versions based on null: the first CSemVer versions will be considered.
                // The alreadyExistingVersion and bestCommitBelow remain null.
                // The possible versions ar the same as the next ones.
                nextPossibleVersions = possibleVersions = GetPossibleVersions( null, null );
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
                    Debug.Assert( basic.UnfilteredThisCommit != null, "This commit is tagged." );
                    // Special case: we have a StartingVersion and it is this commit.
                    if( ExistingVersions.StartingVersionCommit == basic.UnfilteredThisCommit )
                    {
                        Debug.Assert( _startingVersion != null );
                        // alreadyExistingVersion and bestCommitBelow remain null. 
                        possibleVersions = [_startingVersion];
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
        var successors = CSVersion.GetDirectSuccessors( false, baseVersion );
        // Excluding "nex released".
        IEnumerable<TagCommit> allVersions = ExistingVersions.TagCommits;
        if( excluded != null ) allVersions = allVersions.Where( c => c.ThisTag != excluded );
        var nextReleased = allVersions.FirstOrDefault( c => c.ThisTag > baseVersion );
        if( nextReleased != null ) successors = successors.Where( v => v < nextReleased.ThisTag );
        // Restricting StartingVersion.
        if( _startingVersion != null ) successors = successors.Where( v => v >= _startingVersion );
        return successors.ToList();
    }


}
