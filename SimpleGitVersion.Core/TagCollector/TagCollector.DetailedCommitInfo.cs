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

        // If we have a StartingVersion:
        // - A commit exists for it: there's nothing special to do, the current commit point
        //   will be handled normally below (the starting version is considered).
        // - A commit doesn't exist:
        //    - There is no commit at all: the existing versioned commits (if any) are all lower
        //      than our StartingVersion and have been filtered out.
        //      The possible versions are the StartingVersions and we allow its direct successors
        //      (considereing any greater versions would be right but we don't want to concretize and
        //      return a huge list!).
        //    - There are commits: ExistingVersions are necessarily greater than the StartingVerion and
        //      we consider that, as this StartingVersion has never been released to be a new "starting point"
        //      (typically in a new branch from an old commit point for which new versions must be produced - typically
        //      patch or minor). We allow the StartingVersion and its direct successors up to the lowest existing version.
        if( _startingVersion != null && ExistingVersions.StartingVersionCommit == null )
        {
            var v = new List<CSVersion> { _startingVersion };
            if( ExistingVersions.TagCommits.Count == 0 )
            {
                v.AddRange( _startingVersion.GetDirectSuccessors() );
            }
            else
            {
                Debug.Assert( ExistingVersions.TagCommits.All( t => t.ThisTag > _startingVersion ) );
                // Existing versions are in ascending order.
                var nextReleased = ExistingVersions.TagCommits[0].ThisTag;
                v.AddRange( _startingVersion.GetDirectSuccessors().Where( next => next < nextReleased ) );
            }
            possibleVersions = nextPossibleVersions = v;
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
                    if( _startingVersion != null && ExistingVersions.TagCommits.Count == 1 )
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
        IEnumerable<TagCommit> allVersions = ExistingVersions.TagCommits;
        if( excluded != null ) allVersions = allVersions.Where( c => c.ThisTag != excluded );
        var nextReleased = allVersions.FirstOrDefault( c => c.ThisTag > baseVersion );
        var successors = CSVersion.GetDirectSuccessors( false, baseVersion );
        return successors.Where( v => v > _startingVersion
                                          && (nextReleased == null || v < nextReleased.ThisTag) )
                         .ToList();
    }


}
