using CK.Core;
using CK.Text;
using CodeCakeBuilder.Helpers;
using SimpleGitVersion;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CodeCake
{
    public partial class Build
    {
        /// <summary>
        /// Creates a new <see cref="StandardGlobalInfo"/> initialized by the
        /// current environment.
        /// </summary>
        /// <returns>A new info object.</returns>
        static StandardGlobalInfo CreateStandardGlobalInfo( IActivityMonitor m, string? solutionDirectory, CCBOptions cCBOptions )
        {
            var executingAssembly = Assembly.GetEntryAssembly();
            if( solutionDirectory == null && executingAssembly != null )
            {
                solutionDirectory = new Uri( Assembly.GetEntryAssembly().CodeBase ).LocalPath;
                while( System.IO.Path.GetFileName( solutionDirectory ) != "bin" )
                {
                    solutionDirectory = System.IO.Path.GetDirectoryName( solutionDirectory );
                    if( string.IsNullOrEmpty( solutionDirectory ) )
                    {
                        throw new ArgumentException( $"Unable to find /bin/ folder in AppContext.BaseDirectory = {AppContext.BaseDirectory}. Please provide a non null solution directory.", nameof( solutionDirectory ) );
                    }
                }
                solutionDirectory = System.IO.Path.GetDirectoryName( solutionDirectory );
                solutionDirectory = System.IO.Path.GetDirectoryName( solutionDirectory );
            }
            if( solutionDirectory == null ) throw new InvalidOperationException( "Could not load the solution directory." );
            Directory.SetCurrentDirectory( solutionDirectory );
            CommitInfo info = CommitInfo.LoadFromPath( solutionDirectory, new RepositoryInfoOptions() );
            var result = new StandardGlobalInfo( cCBOptions, cCBOptions.InteractiveMode, solutionDirectory, info.FinalBuildInfo );
            // By default:
            if( result.IsValid )
            {
                // gitInfo is valid: it is either ci or a release build. 
                var v = result.BuildInfo.Version;
                // If a /LocalFeed/ directory exists above, we publish the packages in it.
                var localFeedRoot = FileHelpers.FindSiblingDirectoryAbove( solutionDirectory, "LocalFeed" );
                if( localFeedRoot != null )
                {
                    if( v.AsCSVersion == null )
                    {
                        if( v.Prerelease.EndsWith( ".local" ) )
                        {
                            // Local releases must not be pushed on any remote and are copied to LocalFeed/Local
                            // feed (if LocalFeed/ directory above exists).
                            result.IsLocalCIRelease = true;
                            result.LocalFeedPath = System.IO.Path.Combine( localFeedRoot, "Local" );
                        }
                        else
                        {
                            // CI build versions are routed to LocalFeed/CI
                            result.LocalFeedPath = System.IO.Path.Combine( localFeedRoot, "CI" );
                        }
                    }
                    else
                    {
                        // Release or prerelease go to LocalFeed/Release
                        result.LocalFeedPath = System.IO.Path.Combine( localFeedRoot, "Release" );
                    }
                    System.IO.Directory.CreateDirectory( result.LocalFeedPath );
                }
                else result.IsLocalCIRelease = v.Prerelease.EndsWith( ".local" );

                // Creating the right remote feed.
                if( !result.IsLocalCIRelease
                    && (result.InteractiveMode == InteractiveMode.NoInteraction
                        || result.ReadInteractiveOption( m, "PushToRemote", "Push to Remote feeds?", 'Y', 'N' ) == 'Y') )
                {
                    result.PushToRemote = true;
                }
            }
            else
            {
                if( result.InteractiveMode != InteractiveMode.NoInteraction
                    && result.ReadInteractiveOption( m, "PublishDirtyRepo", "Repository is not ready to be published. Proceed anyway?", 'Y', 'N' ) == 'Y' )
                {
                    m.Warn( "Unable to compute a valid version, but you choose to continue..." );
                    result.IgnoreNoArtifactsToProduce = true;
                }
                else
                {
                    // TODO.
                    //// On Appveyor, we let the build run: this gracefully handles Pull Requests.
                    //if( Cake.AppVeyor().IsRunningOnAppVeyor )
                    //{
                    //    result.IgnoreNoArtifactsToProduce = true;
                    //}
                    //else
                    //{
                    //    Cake.TerminateWithError( "Repository is not ready to be published." );
                    //}
                }
                // When the gitInfo is not valid, we do not try to push any packages, even if the build continues
                // (either because the user choose to continue or if we are on the CI server).
                // We don't need to worry about feeds here.
            }
            return result;
        }

    }
}
