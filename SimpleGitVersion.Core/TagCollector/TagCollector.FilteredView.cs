using CSemVer;
using LibGit2Sharp;
using System;
using System.Collections.Generic;

namespace SimpleGitVersion;


sealed partial class TagCollector
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

        public (BasicCommitInfo? Info, bool IsShallowCloned) GetInfo( Commit c )
        {
            string sha = c.Sha;
            if( _cache.TryGetValue( sha, out var d ) ) return (d, d?.IsShallowCloned ?? false);
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
                    return (null, true);
                }
                TagCommit? content = _collector.GetCommit( t.Sha );
                best = content?.GetBestCommitExcept( _excluded );
            }
            var (p, shallow) = ReadParents( c );
            if( best != null || p != null ) d = new BasicCommitInfo( commit, best, p, shallow );
            _cache.Add( sha, d );
            return (d, shallow);
        }

        (BasicCommitInfo? Info, bool IsShallowCloned) ReadParents( Commit c )
        {
            bool shallowHit = false;
            BasicCommitInfo? current = null;
            foreach( var p in c.Parents )
            {
                var (d, shallow) = GetInfo( p );
                shallowHit |= shallow;
                if( current == null || (d != null && d.IsBetterThan( current )) ) current = d;
            }
            return (current, shallowHit);
        }
    }


}
