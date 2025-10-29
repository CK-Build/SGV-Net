using CK.Core;
using CK.Monitoring;
using CSemVer;
using LibGit2Sharp;
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
        Repository.Clone( TestEnv.RemotesPath.AppendPart( "Naked" ), testFolder );
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
                .ShouldContain( "  Working folder has non committed changes." )
                .ShouldContain( "  At least one Modified file found: Naked/Naked.csproj." );
        }
        var versions = File.ReadAllText( nakedProject.Combine( "obj/SimpleGitVersion.txt" ) );
        versions.ShouldBe( $"""
            0.0.0-0
            0.0
            0.0.0.0
            0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z
            {TestHelper.SolutionFolder}/Tests/SimpleGitVersion.MSBuild.Tests/Playground/Remotes/Naked
            
            """ );

        var builtDllPath = nakedProject.Combine( $"{TestHelper.PathToBin}/Naked.dll" );
        var builtVersion = InformationalVersion.ReadFromFile( builtDllPath );
        builtVersion.ToString().ShouldBe( "0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z" );

        var commitDateTime = DateTime.UtcNow;
        ProcessRunner.RunProcess( TestHelper.Monitor, "git", "commit -a -m \"Added SimpleGitVersion.MSBuild package.\" ", testFolder, null )
            .ShouldBe( 0 );

        using( var logs = GrandOutput.Default.ShouldNotBeNull().CreateMemoryCollector( 50 ) )
        {
            ProcessRunner.RunProcess( TestHelper.Monitor, "dotnet", "build", testFolder, null )
                     .ShouldBe( 0 );
            logs.ExtractCurrentTexts()
                .ShouldContain( "  No version information found on or below this commit." );
        }
        builtVersion = InformationalVersion.ReadFromFile( nakedProject.Combine( $"bin/{TestHelper.BuildConfiguration}/Naked.dll" ) );
        builtVersion.ToString().ShouldNotBe( "0.0.0-0/499d526ccd46f92c57293b56530ae1e8d2834ca2/2025-10-28 11:00:58Z" );
        builtVersion.Version.ShouldBe( SVersion.ZeroVersion );
        builtVersion.CommitDate.ShouldBe( commitDateTime, TimeSpan.FromMilliseconds( 100 ) );

    }

}
