using System;
using System.Collections.Generic;
using CSemVer;
using LibGit2Sharp;

namespace SimpleGitVersion
{

    partial class TagCollector
    {
        class FilteredView
        {
            readonly TagCollector _collector;
            readonly CSVersion? _excluded;
            readonly Dictionary<string, BasicCommitInfo?> _cache;

            public FilteredView( TagCollector c, CSVersion? excluded )
            {
                _collector = c;
                _excluded = excluded;
                _cache = new Dictionary<string, BasicCommitInfo?>();
            }

            public BasicCommitInfo? GetInfo( Commit c )
            {
                string sha = c.Sha;
                if( _cache.TryGetValue( sha, out var d ) ) return d;
                TagCommit? commit = _collector.GetCommit( sha );
                ITagCommit? best;
                if( commit != null )
                {
                    best = commit.GetBestCommitExcept( _excluded );
                }
                else
                {
                    Tree t;
                    // Waiting for https://github.com/libgit2/libgit2sharp/issues/1775
                    try
                    {
                        t = c.Tree;
                    }
                    catch( NullReferenceException )
                    {
                        return null;
                    }
                    TagCommit? content = _collector.GetCommit( t.Sha );
                    best = content?.GetBestCommitExcept( _excluded );
                }
                BasicCommitInfo? p = ReadParents( c );
                if( best != null || p != null ) d = new BasicCommitInfo( commit, best, p );
                _cache.Add( sha, d );
                return d;
            }

            BasicCommitInfo? ReadParents( Commit c )
            {
                BasicCommitInfo? current = null;
                foreach( var p in c.Parents )
                {
                    var d = GetInfo( p );
                    if( current == null || (d != null && d.IsBetterThan( current )) ) current = d;
                }
                return current;
            }
        }


    }
}
