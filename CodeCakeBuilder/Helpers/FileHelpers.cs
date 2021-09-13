using CK.Core;
using CK.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CodeCakeBuilder.Helpers
{
    public static class FileHelpers
    {
        public static string? FindSiblingDirectoryAbove( string start, string directoryName )
        {
            if( start == null ) throw new ArgumentNullException( "start" );
            if( directoryName == null ) throw new ArgumentNullException( "directortyName" );
            string? p = Path.GetDirectoryName( start );
            if( string.IsNullOrEmpty( p ) ) return null;
            string pF;
            while( !Directory.Exists( pF = Path.Combine( p, directoryName ) ) )
            {
                p = Path.GetDirectoryName( p );
                if( string.IsNullOrEmpty( p ) ) return null;
            }
            return pF;
        }

        /// <summary>
        /// Helper that deletes a local directory with retries.
        /// Throws the exception after 4 unsuccessful retries.
        /// From https://github.com/libgit2/libgit2sharp/blob/f8e2d42ed9051fa5a5348c1a13d006f0cc069bc7/LibGit2Sharp.Tests/TestHelpers/DirectoryHelper.cs#L40
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="directoryPath">The directory path on the local file system to delete.</param>
        /// <returns>True if the Directory was deleted or did not exist, false if it didn't deleted the directory.</returns>
        public static bool RawDeleteLocalDirectory( IActivityMonitor m, string directoryPath )
        {
            // From http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true/329502#329502

            if( !Directory.Exists( directoryPath ) )
            {
                m.Trace( $"Directory '{directoryPath}' does not exist." );
                return true;
            }
            NormalizeAttributes( directoryPath );
            return DeleteDirectory( m, directoryPath, maxAttempts: 5, initialTimeout: 16, timeoutFactor: 2 );
        }
        static readonly Type[] _whitelist = { typeof( IOException ), typeof( UnauthorizedAccessException ) };
        static bool DeleteDirectory( IActivityMonitor m, string directoryPath, int maxAttempts, int initialTimeout, int timeoutFactor )
        {
            for( int attempt = 1; attempt <= maxAttempts; attempt++ )
            {
                try
                {
                    if( Directory.Exists( directoryPath ) ) Directory.Delete( directoryPath, true );
                    return true;
                }
                catch( Exception ex )
                {
                    var caughtExceptionType = ex.GetType();

                    if( !_whitelist.Any( knownExceptionType => knownExceptionType.GetTypeInfo().IsAssignableFrom( caughtExceptionType ) ) )
                    {
                        throw;
                    }

                    if( attempt < maxAttempts )
                    {
                        Thread.Sleep( initialTimeout * (int)Math.Pow( timeoutFactor, attempt - 1 ) );
                        continue;
                    }

                    m.Trace( string.Format( "{0}The directory '{1}' could not be deleted ({2} attempts were made) due to a {3}: {4}" +
                                                  "{0}Most of the time, this is due to an external process accessing the files in the temporary repositories created during the test runs, and keeping a handle on the directory, thus preventing the deletion of those files." +
                                                  "{0}Known and common causes include:" +
                                                  "{0}- Windows Search Indexer (go to the Indexing Options, in the Windows Control Panel, and exclude the bin folder of LibGit2Sharp.Tests)" +
                                                  "{0}- Antivirus (exclude the bin folder of LibGit2Sharp.Tests from the paths scanned by your real-time antivirus)" +
                                                  "{0}- TortoiseGit (change the 'Icon Overlays' settings, e.g., adding the bin folder of LibGit2Sharp.Tests to 'Exclude paths' and appending an '*' to exclude all subfolders as well)",
                        Environment.NewLine, Path.GetFullPath( directoryPath ), maxAttempts, caughtExceptionType, ex.Message ) );
                }
            }
            return false;
        }

        public static void DirectoryCopy( string sourceDirName, string destDirName, bool copySubDirs )
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo( sourceDirName );

            if( !dir.Exists )
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName );
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if( !Directory.Exists( destDirName ) )
            {
                Directory.CreateDirectory( destDirName );
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach( FileInfo file in files )
            {
                string temppath = Path.Combine( destDirName, file.Name );
                file.CopyTo( temppath, false );
            }

            // If copying subdirectories, copy them and their contents to new location.
            if( copySubDirs )
            {
                foreach( DirectoryInfo subdir in dirs )
                {
                    string temppath = Path.Combine( destDirName, subdir.Name );
                    DirectoryCopy( subdir.FullName, temppath, copySubDirs );
                }
            }
        }

        public static void NormalizeAttributes( string directoryPath )
        {
            string[] filePaths = Directory.GetFiles( directoryPath );
            string[] subdirectoryPaths = Directory.GetDirectories( directoryPath );

            foreach( string filePath in filePaths )
            {
                File.SetAttributes( filePath, FileAttributes.Normal );
            }
            foreach( string subdirectoryPath in subdirectoryPaths )
            {
                NormalizeAttributes( subdirectoryPath );
            }
            File.SetAttributes( directoryPath, FileAttributes.Normal );
        }

        public static void DeleteFiles( IActivityMonitor m, string deletionPattern, string? targetPath = null )
        {
            targetPath ??= Directory.GetCurrentDirectory();
            using( m.OpenTrace( $"Deleting file in directory {targetPath} with pattern {targetPath}." ) )
            {
                Matcher matcher = new();
                matcher.AddInclude( deletionPattern );
                var files = Directory.EnumerateFiles( Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories );
                foreach( var item in matcher.Match( files ).Files )
                {
                    m.Trace( $"Deleting {Path.GetRelativePath( deletionPattern, item.Path )}." );
                    File.Delete( item.Path );
                }
            }
        }

        public static void DeleteDirectories( IActivityMonitor m, IEnumerable<NormalizedPath> paths )
            => DeleteDirectories( m, paths.Select( s => s.Path ) );

        public static void DeleteDirectories( IActivityMonitor m, IEnumerable<string> paths )
        {
            using( m.OpenTrace( "Deleting multiple directories..." ) )
            {
                foreach( string item in paths )
                {
                    if( Directory.Exists( item ) )
                    {
                        Directory.Delete( item, true );
                    }
                }
            }
        }
    }
}
