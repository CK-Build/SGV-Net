using CK.Core;
using CK.Text;
using CodeCake.Abstractions;
using CSemVer;
using Kuinox.TypedCLI.Dotnet;
using SimpleGitVersion;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CodeCake.Build;

namespace CodeCake
{

    public partial class DotnetSolution : ICIPublishWorkflow
    {
        private ArtifactType? _artifactType;

        public ArtifactType ArtifactType
        {
            get
            {
                if( _artifactType == null ) _artifactType = new NuGetArtifactType( _globalInfo, this );
                return _artifactType;
            }
        }

        public async Task<bool> Pack( IActivityMonitor m )
        {
            var nugetInfo = _globalInfo.ArtifactTypes.OfType<NuGetArtifactType>().Single();
            ICommitBuildInfo info = _globalInfo.BuildInfo;
            foreach( var p in nugetInfo.GetNuGetArtifacts() )
            {
                m.Info( p.ArtifactInstance.ToString() );
                bool result = await Dotnet.Pack( m,
                    projectOrSolution: p.ProjectPath,
                    noBuild: true,
                    includeSymbols: true,
                    configuration: _globalInfo.BuildInfo.BuildConfiguration,
                    outputDirectory: _globalInfo.ReleasesFolder,
                    msbuildProperties: new Dictionary<string, string>()
                    {
                        { "CakeBuild","true" },
                        { "Version", info.Version.ToString() },
                        { "AssemblyVersion", info.AssemblyVersion },
                        { "FileVersion", info.FileVersion },
                        { "InformationalVersion", info.InformationalVersion }
                    }
                );
                if( !result ) return false;
            }
            return true;
        }
    }




    public partial class Build
    {
        /// <summary>
        /// Implements NuGet package handling.
        /// </summary>
        public class NuGetArtifactType : ArtifactType
        {
            readonly DotnetSolution _solution;

            public class NuGetArtifact : ILocalArtifact
            {
                public NuGetArtifact( NormalizedPath projectPath, SVersion v )
                {
                    ProjectPath = projectPath;
                    string name = Path.GetFileNameWithoutExtension( projectPath );
                    ArtifactInstance = new ArtifactInstance( "NuGet", name, v );
                }

                public ArtifactInstance ArtifactInstance { get; }

                public NormalizedPath ProjectPath { get; }
            }


            public NuGetArtifactType( StandardGlobalInfo globalInfo, DotnetSolution solution )
                : base( globalInfo, "NuGet" )
            {
                _solution = solution;
            }

            /// <summary>
            /// Downcasts the mutable list <see cref="ILocalArtifact"/> as a set of <see cref="NuGetArtifact"/>.
            /// </summary>
            /// <returns>The set of NuGet artifacts.</returns>
            public IEnumerable<NuGetArtifact> GetNuGetArtifacts() => GetArtifacts().Cast<NuGetArtifact>();

            /// <summary>
            /// Gets the remote target feeds.
            /// </summary>
            /// <returns>The set of remote NuGet feeds (in practice at most one).</returns>
            protected override IEnumerable<ArtifactFeed> GetRemoteFeeds()
            {if( GlobalInfo.BuildInfo.Version.PackageQuality >= CSemVer.PackageQuality.ReleaseCandidate ) yield return new RemoteFeed( this, "nuget.org", "https://api.nuget.org/v3/index.json", "NUGET_ORG_PUSH_API_KEY" );
if( GlobalInfo.BuildInfo.Version.PackageQuality <= CSemVer.PackageQuality.Stable ) yield return new SignatureVSTSFeed( this, "Signature-OpenSource","NetCore3", "Feeds");
}

            /// <summary>
            /// Gets the local target feeds.
            /// </summary>
            /// <returns>The set of remote NuGet feeds (in practice at most one).</returns>
            protected override IEnumerable<ArtifactFeed> GetLocalFeeds()
            {
                return new NuGetHelper.NuGetFeed[] {
                    new NugetLocalFeed( this, GlobalInfo.LocalFeedPath )
                };
            }

            protected override IEnumerable<ILocalArtifact> GetLocalArtifacts()
            {
                return _solution.ProjectsToPublish.Select( p => new NuGetArtifact( p, GlobalInfo.BuildInfo.Version ) );
            }
        }
    }
}
