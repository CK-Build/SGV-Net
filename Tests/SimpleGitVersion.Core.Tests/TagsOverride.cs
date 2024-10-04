using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleGitVersion;

public class TagsOverride
{
    Dictionary<string, IReadOnlyList<string>> _overrides;

    public IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> Overrides
    {
        get { return _overrides; }
    }

    /// <summary>
    /// Adds a set of tags to this <see cref="TagsOverride"/>.
    /// If the commitSha is "head" (instead of a SHA1) the tags are applied on the current head of the repository.
    /// </summary>
    /// <param name="commitSha">The commit or "head".</param>
    /// <param name="tags">Tags to apply.</param>
    /// <returns>This overrides.</returns>
    public TagsOverride MutableAdd( string commitSha, params string[] tags )
    {
        if( _overrides == null )
        {
            _overrides = new Dictionary<string, IReadOnlyList<string>>();
            _overrides.Add( commitSha, new List<string>( tags ) );
        }
        else
        {
            IReadOnlyList<string> exist;
            if( !_overrides.TryGetValue( commitSha, out exist ) )
            {
                _overrides.Add( commitSha, exist = new List<string>() );
            }
            ((List<string>)exist).AddRange( tags );
        }
        return this;
    }

    /// <summary>
    /// Removed a set of tags from this <see cref="TagsOverride"/>.
    /// </summary>
    /// <param name="commitSha">The commit or "head".</param>
    /// <param name="tags">Tags to remove from this overrides.</param>
    /// <returns>This overrides.</returns>
    public TagsOverride MutableRemove( string commitSha, params string[] tags )
    {
        if( _overrides != null && _overrides.TryGetValue( commitSha, out var exist ) )
        {
            ((List<string>)exist).RemoveAll( t => tags.Contains( t ) );
        }
        return this;
    }

    /// <summary>
    /// Adds a set of tags to a new <see cref="TagsOverride"/> instance.
    /// If the commitSha is "head" (instead of a SHA1) the tags are applied on the current head of the repository.
    /// </summary>
    /// <param name="commitSha">The commit or "head".</param>
    /// <param name="tags">Tags to apply.</param>
    /// <returns>A new instance.</returns>
    public TagsOverride Add( string commitSha, params string[] tags )
    {
        TagsOverride n = new TagsOverride();
        if( _overrides != null ) n._overrides = new Dictionary<string, IReadOnlyList<string>>( _overrides );
        n.MutableAdd( commitSha, tags );
        return n;
    }
}
