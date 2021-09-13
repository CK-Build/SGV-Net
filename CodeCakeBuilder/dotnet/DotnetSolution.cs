using CK.Core;
using CK.Text;
using SimpleGitVersion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CodeCake.Abstractions;
using Kuinox.TypedCLI.Dotnet;
using System.Threading.Tasks;
using NuGet.Common;
using CodeCakeBuilder.Helpers;

namespace CodeCake
{

    public static class StandardGlobalInfoDotnetExtension
    {
        public static async Task AddDotnet( this StandardGlobalInfo globalInfo, IActivityMonitor m )
            => globalInfo.RegisterSolution(
                await DotnetSolution.FromSolution( m, globalInfo )
            );

        public static DotnetSolution GetDotnetSolution( this StandardGlobalInfo globalInfo )
            => globalInfo.Solutions.OfType<DotnetSolution>().Single();
    }

    public partial class DotnetSolution : ISolution
    {
        readonly StandardGlobalInfo _globalInfo;
        public string SolutionFileName { get; }
        //public readonly IReadOnlyList<SolutionProject> Projects;
        public IReadOnlyList<NormalizedPath> ProjectsPath { get; }
        public IReadOnlyList<NormalizedPath> ProjectsToPublish { get; }

        DotnetSolution(
            StandardGlobalInfo globalInfo,
            string solutionFileName,
            IEnumerable<NormalizedPath> projects,
            IEnumerable<NormalizedPath> projectsToPublish )
        {
            _globalInfo = globalInfo;
            SolutionFileName = solutionFileName;
            ProjectsPath = projects.ToArray();
            if( ProjectsPath.Count == 0 ) throw new ArgumentException( "There is no project in this solution." );
            ProjectsToPublish = projectsToPublish.ToArray();
        }

        /// <summary>
        /// Create a DotnetSolution from the sln.
        /// </summary>
        /// <param name="globalInfo">The StandardGlobalInfo</param>
        /// <param name="projectToPublishPredicate">
        /// The predicate used to filter the project to publish. By default the predicate is p => !p.Path.Segments.Contains
        /// </param>
        /// <returns></returns>
        internal static async Task<DotnetSolution> FromSolution( IActivityMonitor m, StandardGlobalInfo globalInfo )
        {
            NormalizedPath rootFolder = globalInfo.RootFolder;
            string[] files = Directory.GetFiles( rootFolder, "*.sln" );
            if( files.Length > 1 ) throw new InvalidOperationException( "There is multiple sln at the root folder." ); //TODO: make temp SLN delete on hard exit. (it's probably feasible)
            if( files.Length == 0 ) throw new InvalidOperationException( "There is no sln in the root folder." );
            string solutionFileName = files[0];
            IEnumerable<string>? slnProjects = await Dotnet.Sln.List( m, rootFolder, solutionFileName );
            IEnumerable<NormalizedPath>? projects = slnProjects?.Select( s => new NormalizedPath( Path.GetDirectoryName( s ) ) );
            if( projects == null ) throw new InvalidOperationException( "Could not read projects from the sln." );
            int count = projects.Count();
            projects = projects.Where( s => s.LastPart != "CodeCakeBuilder" );
            if( projects.Count() == count ) throw new InvalidOperationException( "Could not exclude the 'CodeCakeBuilder' is it correctly named ?" );

            List<NormalizedPath> projectsToPublish = projects.Where(
                    p =>
                    {
                        var fullPath = rootFolder.Combine( p );
                        fullPath = fullPath.AppendPart( fullPath.LastPart + ".csproj" );
                        return ((bool?)XDocument.Load( fullPath )
                                                .Root
                                                .Elements( "PropertyGroup" )
                                                .Elements( "IsPackable" ).LastOrDefault() ?? true) == true;
                    }
                //TODO: azure functions are not packable but are published. Something like that.
                // It mean we must add another tag for these case.
                )
                .ToList();

            return new DotnetSolution( globalInfo, solutionFileName, projects, projectsToPublish );
        }

        /// <summary>
        /// Cleans the bin/obj of the projects and the TestResults xml.
        /// </summary>
        public async Task<bool> Clean( IActivityMonitor m )
        {
            using( m.OpenTrace( "Cleaning the dotnet solution..." ) )
            using( var tempSln = TemporarySolutionFile.Create( SolutionFileName ) )
            {
                var exclude = new List<string>() { "CodeCakeBuilder" };
                await tempSln.ExcludeProjectsFromBuild( m, exclude.ToArray() );
                await Dotnet.Clean( m, Path.GetDirectoryName( tempSln.Path )!, tempSln.Path );
                using( m.OpenTrace( "Deleting bin folders..." ) )
                {
                    FileHelpers.DeleteDirectories( m, ProjectsPath.Select( p => p.RemoveLastPart().Combine( "bin" ) ) );
                }
                using( m.OpenTrace( "Deleting obj folders..." ) )
                {
                    FileHelpers.DeleteDirectories( m, ProjectsPath.Select( p => p.RemoveLastPart().Combine( "obj" ) ) );
                }
                using( m.OpenTrace( "Deleting TestResult xml files..." ) )
                {
                    FileHelpers.DeleteFiles( m, "Tests/**/TestResult*.xml" );
                }
            }
            return true;
        }




        /// <summary>
        /// Builds the solution without "CodeCakeBuilder" project itself and
        /// optionally other projects.
        /// </summary>
        /// <param name="excludedProjectName">Optional project names (without path nor .csproj extension).</param>
        public async Task<bool> Build( IActivityMonitor m, params string[] excludedProjectsName )
        {
            using( var tempSln = TemporarySolutionFile.Create( SolutionFileName ) )
            {
                var exclude = new List<string>( excludedProjectsName ) { "CodeCakeBuilder" };
                await tempSln.ExcludeProjectsFromBuild( m, exclude.ToArray() );
                ICommitBuildInfo info = _globalInfo.BuildInfo;
                bool result = await Dotnet.Build( m,
                    projectOrSolution: tempSln.Path,
                    configuration: _globalInfo.BuildInfo.BuildConfiguration,
                    workingDirectory: _globalInfo.RootFolder,
                    noLogo: true,
                    msbuildProperties: new Dictionary<string, string>()
                    {
                        { "CakeBuild","true" },
                        { "Version", info.Version.ToString() },
                        { "AssemblyVersion", info.AssemblyVersion },
                        { "FileVersion", info.FileVersion },
                        { "InformationalVersion", info.InformationalVersion }
                    } );
                return result;
            }
        }

        public Task<bool> Build( IActivityMonitor m ) => Build( m, Array.Empty<string>() );

        public Task<bool> Test( IActivityMonitor m ) => Test( m, Array.Empty<NormalizedPath>() );

        public async Task<bool> Test( IActivityMonitor m, IEnumerable<NormalizedPath>? testProjects = null )
        {
            if( testProjects == null || !testProjects.Any() )
            {
                testProjects = ProjectsPath.Where( p => p.EndsWith( ".Tests" ) );
            }
            using( m.OpenTrace( "Testing projects..." ) )
            {
                foreach( NormalizedPath projectPath in testProjects )
                {
                    using( m.OpenTrace( $"Testing project {projectPath}..." ) )
                    {
                        string projectName = System.IO.Path.GetFileName( projectPath );
                        NormalizedPath binDir = projectPath.AppendPart( "bin" ).AppendPart( _globalInfo.BuildInfo.BuildConfiguration );
                        NormalizedPath objDir = projectPath.AppendPart( "obj" );
                        string assetsJson = File.ReadAllText( objDir.AppendPart( "project.assets.json" ) );
                        bool isNunitLite = assetsJson.Contains( "NUnitLite" );
                        if( isNunitLite ) m.Trace( "'NUnitLite' string detected in 'project.assets.json', this is probably a test project usign NunitLite." );
                        bool isVSTest = assetsJson.Contains( "Microsoft.NET.Test.Sdk" );
                        if( isVSTest ) m.Trace( "'Microsoft.NET.Test.Sdk' string detected in 'project.assets.json', this is probably a test project using VSTest." );
                        foreach( NormalizedPath buildDir in
                            Directory.GetDirectories( binDir )
                                .Where( p => Directory.EnumerateFiles( p ).Any() )
                        )
                        {
                            string runtimeIdentifier = buildDir.LastPart;
                            using( m.OpenTrace( $"Running tests in {runtimeIdentifier}." ) )
                            {
                                bool isNetFramework =
                                    runtimeIdentifier.StartsWith( "net" )
                                    && int.TryParse( runtimeIdentifier.Substring( 3 ), NumberStyles.Float, CultureInfo.InvariantCulture, out int frameworkVersion )
                                    && frameworkVersion < 5;
                                if( isNetFramework ) m.Trace( "Detected legacy .NET Framework." );
                                string fileWithoutExtension = buildDir.AppendPart( projectName );
                                string testBinariesPath;
                                if( isVSTest )
                                {
                                    testBinariesPath = fileWithoutExtension + ".dll";
                                    // VS Tests
                                    using( var grp = m.OpenInfo( $"Testing via VSTest ({runtimeIdentifier}): {testBinariesPath}" ) )
                                    {

                                        if( !_globalInfo.CheckCommitMemoryKey( m, testBinariesPath ) )
                                        {
                                            bool result = await Dotnet.Test( m,
                                                projectOrSolutionOrDirectoryOrDll: testBinariesPath,
                                                workingDirectory: _globalInfo.RootFolder,
                                                noBuild: true,
                                                noRestore: true,
                                                framework: runtimeIdentifier,
                                                configuration: _globalInfo.BuildInfo.BuildConfiguration );
                                            if( !result ) return false;
                                            _globalInfo.WriteCommitMemoryKey( testBinariesPath ?? throw new InvalidOperationException( nameof( testBinariesPath ) + "was not set." ) );
                                        }
                                        else
                                        {
                                            grp.ConcludeWith( () => "The commit memory key was present: test has already been successfully completed." );
                                        }
                                    }
                                    continue;
                                }

                                if( isNunitLite )
                                {
                                    // Using NUnitLite.
                                    if( isNetFramework && File.Exists( testBinariesPath = fileWithoutExtension + ".exe" ) )
                                    {
                                        using( var grp = m.OpenInfo( $"Testing via NUnitLite ({runtimeIdentifier}): {testBinariesPath}" ) )
                                        {

                                            if( !_globalInfo.CheckCommitMemoryKey( m, testBinariesPath ) )
                                            {
                                                bool result = await CLIRunner.RunAsync( m, "nunit3-console.exe", new string[] { "--result=\"TestResult.Net461.xml\"" }, buildDir );
                                                if( !result ) return false;
                                                _globalInfo.WriteCommitMemoryKey( testBinariesPath ?? throw new InvalidOperationException( nameof( testBinariesPath ) + "was not set." ) );
                                            }
                                            else
                                            {
                                                grp.ConcludeWith( () => "The commit memory key was present: test has already been successfully completed." );
                                            }
                                        }
                                    }
                                    else
                                    {
                                        testBinariesPath = fileWithoutExtension + ".dll";
                                        using( var grp = m.OpenInfo( $"Testing via NUnitLite ({runtimeIdentifier}): {testBinariesPath}" ) )
                                        {
                                            if( !_globalInfo.CheckCommitMemoryKey( m, testBinariesPath ) )
                                            {
                                                if( !await Dotnet.Run( m, thingToRun: testBinariesPath, workingDirectory: buildDir ) ) return false;
                                                _globalInfo.WriteCommitMemoryKey( testBinariesPath ?? throw new InvalidOperationException( nameof( testBinariesPath ) + "was not set." ) );
                                            }
                                            else
                                            {
                                                grp.ConcludeWith( () => "The commit memory key was present: test has already been successfully completed." );
                                            }
                                        }
                                    }
                                    continue;
                                }
                                testBinariesPath = fileWithoutExtension + ".dll";
                                using( var grp = m.OpenInfo( $"Testing via NUnit: {testBinariesPath}" ) )
                                {
                                    if( !_globalInfo.CheckCommitMemoryKey( m, testBinariesPath ) )
                                    {
                                        bool result = await CLIRunner.RunAsync( m, "nunit-console.exe",
                                            new string[] { $"-framework:v4.5 -result:{projectPath.AppendPart( "TestResult.Net461.xml" )}" },
                                            workingDirectory: buildDir
                                        );
                                        if( !result ) return false;
                                    }
                                    else
                                    {
                                        grp.ConcludeWith( () => "The commit memory key was present: test has already been successfully completed." );
                                    }
                                }
                                _globalInfo.WriteCommitMemoryKey( testBinariesPath ?? throw new InvalidOperationException( nameof( testBinariesPath ) + "was not set." ) );
                            }
                        }
                    }
                }
            }
            return true;
        }
    }
}
