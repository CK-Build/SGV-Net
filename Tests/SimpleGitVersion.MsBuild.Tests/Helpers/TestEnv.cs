using CK.Core;
using CSemVer;
using NUnit.Framework;
using Shouldly;
using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using static CK.Testing.MonitorTestHelper;

namespace SimpleGitVersion.MSBuild.Tests;

[SetUpFixture]
static partial class TestEnv
{
    readonly static NormalizedPath _playgroundPath = TestHelper.TestProjectFolder.AppendPart( "Playground" );
    readonly static NormalizedPath _remotesPath = _playgroundPath.AppendPart( "Remotes" );
    readonly static NormalizedPath _nugetSourcePath = _playgroundPath.AppendPart( "NuGetSource" );
    readonly static NormalizedPath _clonedPath = _playgroundPath.AppendPart( "Cloned" );
    static SVersion? _sgvPackageVersion;

    [OneTimeSetUp]
    public static void SetupEnv() => TestHelper.OnlyOnce( Initialize );

    [OneTimeTearDown]
    public static void TearDownEnv()
    {
    }

    static void Initialize()
    {
        InitializeRemotes();
        InitializeNuGetSource();
    }

    static void InitializeNuGetSource()
    {
        TestHelper.CleanupFolder( _nugetSourcePath );

        NuGetHelper.ClearGlobalCache( TestHelper.Monitor, "SimpleGitVersion.MSBuild", null ).ShouldBeTrue();

        //
        // Unfortunately: https://github.com/dotnet/msbuild/issues/4303#issuecomment-3460008540
        //
        // So we MUST <GenerateDocumentationFile>true</GenerateDocumentationFile> here otherwise
        // in CI builds, executing the test doesn't produce the .xml file and dotnet pack fails
        // because it expects the xml file exist (and that is a good thing!).
        //
        // BUT: The build is also done with versions...
        // => We MUST NOT recompile anything in CI.
        //
        // When debugging however, this is very helpful... So let's consider the fact that ONLY WHEN a
        // debugger is attached, we rebuild all. 
        if( Debugger.IsAttached )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor.ParallelLogger,
                                      "dotnet",
                                      $"build -c {TestHelper.BuildConfiguration} --no-restore -tl:off --nologo",
                                      TestHelper.SolutionFolder.AppendPart( "SimpleGitVersion.Core" ),
                                      null )
                         .ShouldBe( 0 );
            ProcessRunner.RunProcess( TestHelper.Monitor.ParallelLogger,
                                      "dotnet",
                                      $"pack -c {TestHelper.BuildConfiguration} --no-restore -tl:off --nologo -o \"{_nugetSourcePath}\"",
                                      TestHelper.SolutionFolder.AppendPart( "SimpleGitVersion.MSBuild" ),
                                      null )
                         .ShouldBe( 0 );
        }
        else
        {
            ProcessRunner.RunProcess( TestHelper.Monitor.ParallelLogger,
                                      "dotnet",
                                      $"pack -c {TestHelper.BuildConfiguration} --no-restore --no-build -tl:off --nologo -o \"{_nugetSourcePath}\"",
                                      TestHelper.SolutionFolder.AppendPart( "SimpleGitVersion.MSBuild" ),
                                      null )
                         .ShouldBe( 0 );
        }
        var packageName = Path.GetFileNameWithoutExtension( Directory.EnumerateFiles( _nugetSourcePath ).Single() );
        _sgvPackageVersion = SVersion.Parse( packageName["SimpleGitVersion.MSBuild.".Length..] );
    }

    static void InitializeRemotes()
    {
        var zipPath = _remotesPath.AppendPart( "Remotes.zip" );
        var zipTime = File.GetLastWriteTimeUtc( zipPath );
        if( !Directory.EnumerateDirectories( _remotesPath ).Any()
            || LastWriteTimeChanged( zipTime ) )
        {
            using( TestHelper.Monitor.OpenInfo( $"Last write time of 'Remotes/' differ from 'Remotes/Remotes.zip'. Restoring remotes from zip." ) )
            {
                foreach( var remotes in Directory.EnumerateDirectories( _remotesPath ) )
                {
                    foreach( var repository in Directory.EnumerateDirectories( remotes ) )
                    {
                        RemoveAllReadOnlyAttribute( repository );
                        TestHelper.CleanupFolder( repository, ensureFolderAvailable: false );
                    }
                }
                // Allow overwriting .gitignore file.
                ZipFile.ExtractToDirectory( zipPath, _remotesPath, overwriteFiles: true );
                SetLastWriteTime( zipTime );
            }
        }

        static bool LastWriteTimeChanged( DateTime zipTime )
        {
            if( Directory.GetLastWriteTimeUtc( _remotesPath ) != zipTime )
            {
                return true;
            }
            foreach( var sub in Directory.EnumerateDirectories( _remotesPath ) )
            {
                if( Directory.GetLastWriteTimeUtc( sub ) != zipTime )
                {
                    return true;
                }
            }
            return false;
        }

        static void SetLastWriteTime( DateTime zipTime )
        {
            Directory.SetLastWriteTimeUtc( _remotesPath, zipTime );
            foreach( var sub in Directory.EnumerateDirectories( _remotesPath ) )
            {
                Directory.SetLastWriteTimeUtc( sub, zipTime );
            }
        }
    }

    /// <summary>
    /// Gets the the version of "SimpleGitVersion.MSBuild" package available in the "NuGetSource/" folder.
    /// </summary>
    public static SVersion SGVPackageVersion => _sgvPackageVersion!;

    public static NormalizedPath RemotesPath => _remotesPath;

    public static NormalizedPath NugetSourcePath => _nugetSourcePath;

    public static NormalizedPath EnsureCleanFolder( [CallerMemberName] string? name = null )
    {
        var path = _clonedPath.AppendPart( name );
        if( Directory.Exists( path ) )
        {
            RemoveAllReadOnlyAttribute( path );
            TestHelper.CleanupFolder( path, ensureFolderAvailable: true );
        }
        else
        {
            Directory.CreateDirectory( path );
        }
        return path;

    }

    static void RemoveAllReadOnlyAttribute( string folder )
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = false,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.System
        };
        foreach( var f in Directory.EnumerateFiles( folder, "*", options ) )
        {
            File.SetAttributes( f, FileAttributes.Normal );
        }
    }

}
