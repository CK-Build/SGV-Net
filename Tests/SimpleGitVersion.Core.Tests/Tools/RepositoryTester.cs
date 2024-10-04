using LibGit2Sharp;
using System.Collections.Generic;
using System.Linq;

namespace SimpleGitVersion;

class RepositoryTester
{
    public readonly string Path;
    readonly List<SimpleCommit> _commits;

    public RepositoryTester( string path )
    {
        Path = path;
        using( var r = new Repository( path ) )
        {
            _commits = r.Commits.QueryBy( new CommitFilter { IncludeReachableFrom = r.Refs } ).Select( c => new SimpleCommit() { Sha = c.Sha, Message = c.Message } ).ToList();
        }
    }

    public IReadOnlyList<SimpleCommit> Commits { get { return _commits; } }

    public void CheckOut( string branchName )
    {
        using( var r = new Repository( Path ) )
        {
            Branch b = r.Branches[branchName];
            Commands.Checkout( r, b, new CheckoutOptions() { CheckoutModifiers = CheckoutModifiers.Force } );
        }
    }

    public CommitInfo GetRepositoryInfo( RepositoryInfoOptions options = null )
    {
        return CommitInfo.LoadFromPath( Path, options );
    }

    public CommitInfo GetRepositoryInfo( string headCommit, TagsOverride tags = null )
    {
        return CommitInfo.LoadFromPath( Path, new RepositoryInfoOptions { HeadCommit = headCommit, OverriddenTags = tags != null ? tags.Overrides : null } );
    }
}

