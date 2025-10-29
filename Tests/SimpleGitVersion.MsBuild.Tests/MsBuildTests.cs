using CK.Core;
using CK.Monitoring;
using CSemVer;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using static CK.Testing.MonitorTestHelper;

namespace SimpleGitVersion.MSBuild.Tests;

[TestFixture]
public class MSBuildTests
{
    [Test]
    public void from_scratch_test()
    {
        var testFolder = TestEnv.EnsureCleanFolder();

        ProcessRunner.RunProcess( TestHelper.Monitor, "git", $"clone \"{TestEnv.RemotesPath.AppendPart( "Naked" )}\" \"{testFolder}\"", testFolder, null )
            .ShouldBe( 0 );

        NuGetHelper.EnsureNuGetConfigFile( testFolder );

        var nakedProject = testFolder.AppendPart( "Naked" );
        var sourcePath = TestEnv.NugetSourcePath.Path.Replace( NormalizedPath.DirectorySeparatorChar, Path.DirectorySeparatorChar );

        ProcessRunner.RunProcess( TestHelper.Monitor.ParallelLogger,
                                  "dotnet",
                                  // Will be "package add .." in SDK .net10.
                                  $"add package SimpleGitVersion.MSBuild -v {TestEnv.SGVPackageVersion} -s \"{sourcePath}\"",
                                  nakedProject, null )
                     .ShouldBe( 0 );

        using( var logs = GrandOutput.Default.ShouldNotBeNull().CreateMemoryCollector( 50 ) )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor, "dotnet", "build", testFolder, null )
                     .ShouldBe( 0 );
            logs.ExtractCurrentTexts()
                .ShouldContain( "  | [Error] Working folder has non committed changes." )
                .ShouldContain( "  |         At least one Modified file found: Naked/Naked.csproj." )
                .ShouldContain( "  |=> 0.0.0-0 (Configuration: Debug)" );
        }
        var versions = File.ReadAllText( nakedProject.Combine( "obj/SimpleGitVersionFile.txt" ) );
        var expected = $"""
            0.0.0-0
            0.0
            0.0.0.0
            0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z
            Debug
            {TestHelper.SolutionFolder}/Tests/SimpleGitVersion.MsBuild.Tests/Playground/Remotes/Naked
            
            """;
        versions.ShouldBe( expected );

        var builtDebugDllPath = nakedProject.Combine( $"bin/Debug/net8.0/Naked.dll" );
        var builtReleaseDllPath = nakedProject.Combine( $"bin/Release/net8.0/Naked.dll" );

        var builtVersion = InformationalVersion.ReadFromFile( builtDebugDllPath );
        builtVersion.IsValidSyntax.ShouldBeTrue();
        builtVersion.ToString().ShouldBe( "0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z" );

        var commitDateTime = DateTime.UtcNow;
        ProcessRunner.RunProcess( TestHelper.Monitor, "git", "commit -a -m \"Added SimpleGitVersion.MsBuild package.\" ", testFolder, null )
            .ShouldBe( 0 );

        using( var logs = GrandOutput.Default.ShouldNotBeNull().CreateMemoryCollector( 50 ) )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor, "dotnet", "build", testFolder, null )
                     .ShouldBe( 0 );
            logs.ExtractCurrentTexts()
                .ShouldContain( "  | No version information found on or below this commit." )
                .ShouldContain( "  |=> 0.0.0-0 (Configuration: Debug)" );
        }
        builtVersion = InformationalVersion.ReadFromFile( builtDebugDllPath );
        builtVersion.IsValidSyntax.ShouldBeTrue();
        builtVersion.ToString().ShouldNotBe( "0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z" );
        builtVersion.Version.ShouldBe( SVersion.ZeroVersion );
        builtVersion.CommitDate.ShouldBe( commitDateTime, TimeSpan.FromSeconds( 1 ) );

        ProcessRunner.RunProcess( TestHelper.Monitor, "git", "tag v1.2.3-alpha", testFolder, null )
            .ShouldBe( 0 );

        using( var logs = GrandOutput.Default.ShouldNotBeNull().CreateMemoryCollector( 50 ) )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor, "dotnet", "build", testFolder, null )
                     .ShouldBe( 0 );
            logs.ExtractCurrentTexts()
                .ShouldContain( "  | Tag: 1.2.3-a" )
                .ShouldContain( "  | No base tag found below this commit." )
                .ShouldContain( "  | Possible version(s) : 0.0.0-a, 0.0.0-b, 0.0.0-d, 0.0.0-e, 0.0.0-g, 0.0.0-k, 0.0.0-p, 0.0.0-r, 0.0.0, 0.1.0-a, 0.1.0-b, 0.1.0-d, 0.1.0-e, 0.1.0-g, 0.1.0-k, 0.1.0-p, 0.1.0-r, 0.1.0, 1.0.0-a, 1.0.0-b, 1.0.0-d, 1.0.0-e, 1.0.0-g, 1.0.0-k, 1.0.0-p, 1.0.0-r, 1.0.0" )
                .ShouldContain( "  |=> 0.0.0-0 (Configuration: Debug)" );
        }

        ProcessRunner.RunProcess( TestHelper.Monitor, "git", "tag -d v1.2.3-alpha", testFolder, null )
            .ShouldBe( 0 );

        ProcessRunner.RunProcess( TestHelper.Monitor, "git", "tag v0.1.0-rc", testFolder, null )
            .ShouldBe( 0 );

        using( var logs = GrandOutput.Default.ShouldNotBeNull().CreateMemoryCollector( 50 ) )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor, "dotnet", "build -c Release", testFolder, null )
                     .ShouldBe( 0 );
            logs.ExtractCurrentTexts()
                .ShouldContain( "  | Tag: 0.1.0-r" )
                .ShouldContain( "  |=> 0.1.0-r (Configuration: Release)" );
        }

        builtVersion = InformationalVersion.ReadFromFile( builtReleaseDllPath );
        builtVersion.IsValidSyntax.ShouldBeTrue();
        builtVersion.Version.ToString().ShouldBe( "0.1.0-r" );
        builtVersion.CommitDate.ShouldBe( commitDateTime, TimeSpan.FromSeconds( 1 ) );
    }

}
