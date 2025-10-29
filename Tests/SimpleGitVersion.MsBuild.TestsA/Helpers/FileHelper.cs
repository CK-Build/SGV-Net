using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SimpleGitVersion.MSBuild.Tests;

/// <summary>
/// File system related helpers.
/// </summary>
public sealed class FileHelper
{
    /// <summary>
    /// Standard helper with 5 retries. Ultimately gives up with a logged error and returns false.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="path">The file path to delete.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool DeleteFile( IActivityMonitor monitor, string path )
    {
        int tryCount = 0;
        for(; ; )
        {
            try
            {
                if( File.Exists( path ) ) File.Delete( path );
                return true;
            }
            catch( Exception ex )
            {
                if( ++tryCount > 5 )
                {
                    monitor.Error( $"While trying to delete file '{path}'.", ex );
                    return false;
                }
                Thread.Sleep( 100 );
            }
        }
    }

    /// <summary>
    /// Moves/renames a directory. This handles casing correctly (even on case insenstive file systems).
    /// <para>
    /// Retries up to 5 times and ultimately gives up with a logged error and returns false.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="from">The source folder to move.</param>
    /// <param name="to">The target folder.</param>
    /// <param name="potentiallyEmptyFolders">When provided, thi set is filled with folders that may be empty and may need to be suppressed.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool TryMoveFolder( IActivityMonitor monitor,
                                      NormalizedPath from,
                                      NormalizedPath to,
                                      HashSet<NormalizedPath>? potentiallyEmptyFolders = null )
    {
        int tryCount = 0;
        for(; ; )
        {
            try
            {
                if( Directory.Exists( to ) )
                {
                    // See https://stackoverflow.com/questions/1622597/renaming-directory-with-same-name-different-case
                    if( from.Path.Equals( to.Path, StringComparison.OrdinalIgnoreCase ) )
                    {
                        var tempName = to + "[__CASING]";
                        Directory.Move( from, tempName );
                        Directory.Move( tempName, to );
                        return true;
                    }
                    monitor.Error( $"The target folder '{to}' exists. Failed to move '{from}'." );
                    return false;
                }
                var parent = to.RemoveLastPart();
                if( !Directory.Exists( parent ) ) Directory.CreateDirectory( parent );
                Directory.Move( from, to );
                potentiallyEmptyFolders?.Add( from.RemoveLastPart() );
                return true;
            }
            catch( Exception ex )
            {
                if( ++tryCount > 5 )
                {
                    monitor.Error( $"While moving folder from '{from}' to '{to}'.", ex );
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Not so standard helper as this can delete a git working folder by removing the read-only attributes on ".git/objects/" files
    /// before attempting to delete the folder. Only ".git/objects/" files are handled: if the folder contains other read-only files,
    /// this fails.
    /// <para>
    /// Retries up to 5 times and ultimately gives up with a logged error and returns false.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="path">The directory path to delete.</param>
    /// <returns>True on success, false on error.</returns>
    public static bool DeleteFolder( IActivityMonitor monitor, string path )
    {
        if( !DeleteClonedFolderOnly( monitor, path, out bool isClonedFolder ) )
        {
            if( isClonedFolder ) return false;
            foreach( var d in Directory.EnumerateDirectories( path ) )
            {
                if( !DeleteFolder( monitor, d ) )
                {
                    return false;
                }
            }
            return DoDeleteFolder( monitor, path );
        }
        return true;
    }

    static bool DeleteClonedFolderOnly( IActivityMonitor monitor, string path, out bool isClonedFolder )
    {
        isClonedFolder = false;
        var gitObjectsPath = Path.Combine( path, ".git", "objects" );
        if( Directory.Exists( gitObjectsPath ) )
        {
            isClonedFolder = true;
            foreach( var hash in Directory.EnumerateDirectories( gitObjectsPath ) )
            {
                foreach( var gitObjectFile in Directory.EnumerateFiles( hash ) )
                {
                    File.SetAttributes( gitObjectFile, FileAttributes.Normal );
                }
            }
            return DoDeleteFolder( monitor, path );
        }
        return false;
    }

    static bool DoDeleteFolder( IActivityMonitor monitor, string path )
    {
        int tryCount = 0;
        for(; ; )
        {
            try
            {
                if( Directory.Exists( path ) ) Directory.Delete( path, true );
                return true;
            }
            catch( Exception ex )
            {
                if( ++tryCount > 5 )
                {
                    monitor.Error( $"While trying to delete folder '{path}'.", ex );
                    return false;
                }
                Thread.Sleep( 100 );
            }
        }
    }

}
