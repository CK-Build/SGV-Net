using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using CSemVer;

namespace SimpleGitVersion
{
    /// <summary>
    /// Discovers existing tags in the repository and provide a <see cref="GetCommitInfo"/> to
    /// retrieve commit information.
    /// </summary>
    partial class TagCollector
    {
        readonly CSVersion? _startingVersion;
        readonly Dictionary<string, TagCommit> _collector;
        readonly RepositoryVersions? _repoVersions;

        /// <summary>
        /// Gets the minimal version to consider. When null, the whole repository must be valid in terms of release tags.
        /// </summary>
        public CSVersion? StartingVersion => _startingVersion;

        /// <summary>
        /// See <see cref="CommitInfo.ExistingVersions"/>.
        /// </summary>
        public RepositoryVersions? ExistingVersions => _repoVersions;

        public readonly CommitInfo.ErrorCodeStatus ErrorCode;

        /// <summary>
        /// Initializes a new <see cref="TagCollector"/>.
        /// Errors may be appended to the collector that can be syntaxic errors or multiple different versions applied to the same commit point.
        /// </summary>
        /// <param name="errors">A collector of errors. One line per error.</param>
        /// <param name="repo">The Git repository.</param>
        /// <param name="startingVersion">Vesion tags lower than this version will be ignored.</param>
        /// <param name="overriddenTags">Optional commits with associated tags that are applied as if they exist in the repository.</param>
        /// <param name="singleMajor">Optional major filter.</param>
        /// <param name="checkExistingVersions">True to check existing </param>
        internal TagCollector(
            StringBuilder errors,
            Repository repo,
            string? startingVersion = null,
            IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>? overriddenTags = null,
            int? singleMajor = null,
            bool checkExistingVersions = false )
        {
            _collector = new Dictionary<string, TagCommit>();

            if( startingVersion != null )
            {
                var v = CSVersion.TryParse( startingVersion, true );
                if( !v.IsValid )
                {
                    ErrorCode = CommitInfo.ErrorCodeStatus.InvalidStartingVersion;
                    errors.Append( "Invalid StartingVersion. " ).Append( v.ErrorMessage ).AppendLine();
                    return;
                }
                _startingVersion = v.ToNormalizedForm();
                if( singleMajor.HasValue && _startingVersion.Major > singleMajor )
                {
                    ErrorCode = CommitInfo.ErrorCodeStatus.StartingVersionConflictsWithSingleMajor;
                    errors.Append( "StartingVersion '" )
                          .Append( _startingVersion )
                          .Append( "'is defined, its major must not be greater than defined SingleMajor = " ).Append( singleMajor ).Append( "." )
                          .AppendLine();
                    return;
                }
            }
            // Register all tags.
            ErrorCode = RegisterAllTags( errors, repo, overriddenTags, singleMajor );
            if( ErrorCode != CommitInfo.ErrorCodeStatus.None ) return;

            // Resolves multiple tags on the same commit.
            if( !CloseCollect( errors ) )
            {
                ErrorCode = CommitInfo.ErrorCodeStatus.MultipleVersionTagConflict;
                return;
            }

            // Sorts TagCommit, optionally checking the existing versions. 
            _repoVersions = new RepositoryVersions( _collector.Values, errors );

            if( checkExistingVersions
                && (ErrorCode = _repoVersions.CheckExistingVersions( errors, _startingVersion )) != CommitInfo.ErrorCodeStatus.None )
            {
                return;
            }

            // Register content (if no error occurred).
            foreach( var tc in _repoVersions.TagCommits )
            {
                RegisterContent( tc );
            }
        }

        CommitInfo.ErrorCodeStatus RegisterAllTags( StringBuilder errors, Repository repo, IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>? overriddenTags, int? singleMajor )
        {
            foreach( var tag in repo.Tags )
            {
                Commit? tagCommit = tag.PeeledTarget as Commit;
                if( tagCommit == null ) continue;
                RegisterOneTag( tagCommit, tag.FriendlyName, singleMajor );
            }

            // Applies overrides (if any) as if they exist in the repository.
            if( overriddenTags != null )
            {
                foreach( var k in overriddenTags )
                {
                    Commit? o = null;
                    if( string.IsNullOrEmpty( k.Key ) )
                    {
                        errors.Append( "Invalid overridden commit: the key is null or empty." ).AppendLine();
                        return CommitInfo.ErrorCodeStatus.InvalidOverriddenTag;
                    }
                    else if( k.Key.Equals( "head", StringComparison.OrdinalIgnoreCase ) )
                    {
                        o = repo.Head.Tip;
                        Debug.Assert( o != null, "Uninitialized Git repository. Already handled." );
                    }
                    else
                    {
                        o = repo.Lookup<Commit>( k.Key );
                        if( o == null )
                        {
                            errors.AppendFormat( "Overridden commit '{0}' does not exist.", k.Key ).AppendLine();
                            return CommitInfo.ErrorCodeStatus.InvalidOverriddenTag;
                        }
                    }
                    if( o != null )
                    {
                        foreach( string tagName in k.Value )
                        {
                            RegisterOneTag( o, tagName, singleMajor );
                        }
                    }
                }
            }
            return CommitInfo.ErrorCodeStatus.None;
        }

        void RegisterOneTag( Commit c, string tagName, int? singleMajor )
        {
            CSVersion v = CSVersion.TryParse( tagName ).ToNormalizedForm();
            if( v.IsValid && (!singleMajor.HasValue || v.Major == singleMajor.Value) )
            {
                if( _startingVersion != null && _startingVersion.CompareTo( v ) > 0 )
                {
                    // This version is smaller than the StartingVersion: we ignore it.
                    return;
                }
                if( _collector.TryGetValue( c.Sha, out var tagCommit ) )
                {
                    tagCommit.AddCollectedTag( v );
                }
                else _collector.Add( c.Sha, new TagCommit( c, v ) );
            }
        }

        bool CloseCollect( StringBuilder errors )
        {
            Debug.Assert( errors.Length == 0 && ErrorCode == CommitInfo.ErrorCodeStatus.None );
            List<TagCommit>? invalidTags = null;
            foreach( var c in _collector.Values )
            {
                if( !c.CloseCollect( errors ) )
                {
                    if( invalidTags == null ) invalidTags = new List<TagCommit>();
                    invalidTags.Add( c );
                }
            }
            if( invalidTags != null )
            {
                foreach( var c in invalidTags )
                {
                    _collector.Remove( c.CommitSha );
                }
            }
            return errors.Length == 0;
        }

        void RegisterContent( TagCommit tagCommit )
        {
            TagCommit contentExists;
            if( _collector.TryGetValue( tagCommit.ContentSha, out contentExists ) )
            {
                if( tagCommit != contentExists ) contentExists.AddSameTree( tagCommit );
            }
            else _collector.Add( tagCommit.ContentSha, tagCommit );
        }

        TagCommit? GetCommit( string sha )
        {
            TagCommit t;
            _collector.TryGetValue( sha, out t );
            return t;
        }
    }   
}
