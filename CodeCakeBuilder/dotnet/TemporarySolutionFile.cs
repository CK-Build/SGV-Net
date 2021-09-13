using CK.Core;
using CK.Text;
using Kuinox.TypedCLI.Dotnet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeCake
{
    /// <summary>
    /// Internal implementation of <see cref="ITemporarySolutionFile"/>.
    /// Use <see cref="CodeCakeSolutionExtensions.CreateTemporarySolutionFile(ICakeContext, FilePath)">CreateTemporarySolutionFile</see> extension method to obtain a concrete implementation.
    /// </summary>
    class TemporarySolutionFile : ITemporarySolutionFile
    {

        TemporarySolutionFile( NormalizedPath originalPath, NormalizedPath modifiedPath )
        {
            OriginalPath = originalPath;
            Path = modifiedPath;
        }

        public static ITemporarySolutionFile Create( NormalizedPath originalPath )
        {
            originalPath = System.IO.Path.GetFullPath( originalPath );
            string modifiedPath = originalPath + Guid.NewGuid().ToString( "N" ) + ".sln";
            File.Copy( originalPath, modifiedPath );
            File.SetAttributes( modifiedPath, FileAttributes.Temporary );
            return new TemporarySolutionFile( originalPath, modifiedPath );
        }

        public NormalizedPath OriginalPath { get; }

        public NormalizedPath Path { get; }

        public void Dispose() => File.Delete( Path );

        public Task ExcludeProjectsFromBuild( IActivityMonitor m, params string[] projectNames )
            => ExcludeProjectsFromBuild( m, (IEnumerable<string>)projectNames );

        public Task ExcludeProjectsFromBuild( IActivityMonitor m, IEnumerable<string> projectNames )
            => Dotnet.Sln.Remove( m, projectNames, slnName: Path );
    }
}
