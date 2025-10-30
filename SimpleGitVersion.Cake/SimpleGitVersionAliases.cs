using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;
using System;

namespace SimpleGitVersion;

/// <summary>
/// Contains functionality related to SimpleGitVersion.
/// </summary>
[CakeAliasCategory( "SimpleGitVersion" )]
public static class SimpleGitVersionAliases
{
    /// <summary>
    /// Gets a <see cref="CommitInfo"/> object computed from the current head of the Git repository.
    /// By default, the RepositoryInfo.xml file at the root is used (if it exists) to obtain the <paramref name="options"/>.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="options">Optional options.</param>
    /// <returns>A RepositoryInfo object.</returns>
    [CakeMethodAlias]
    public static CommitInfo GetRepositoryInfo( this ICakeContext context, RepositoryInfoOptions? options = null )
    {
        if( context == null ) throw new ArgumentNullException( nameof( context ) );
        var logger = new StringLogger();
        var path = context.Environment.WorkingDirectory.FullPath;
        var r = options != null
                ? CommitInfo.LoadFromPath( path, options )
                : CommitInfo.LoadFromPath( logger, path );
        r.Explain( logger );
        context.Log.Information( Verbosity.Quiet, logger.Conclude( r.FinalBuildInfo ) );
        return r;
    }

}
