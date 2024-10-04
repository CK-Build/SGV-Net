using CSemVer;
using FluentAssertions;
using NUnit.Framework;
using System.Linq;

namespace SimpleGitVersion.Core.Tests;

[TestFixture]
public class SavingThe0000LastReleaseBasedVersionTests
{
    static readonly RepositoryTester repoTest = TestHelper.TestGitRepository;
    // Those 4 commits have the same content.
    static readonly SimpleCommit cTop = repoTest.Commits.First( sc => sc.Sha == "fc9802013c23398978744de1618fb01638f7347e" );   // origin/parallel-world
    static readonly SimpleCommit cGamma = repoTest.Commits.First( sc => sc.Sha == "a1dd5450f75f4e76179176335002816009b549c8" ); // |\ origin/gamma
    static readonly SimpleCommit cBeta = repoTest.Commits.First( sc => sc.Sha == "9f682cd99fae84cceb0ccbd8a9a4ab5647e1b420" );  // | |\ origin/beta
    static readonly SimpleCommit cBase = repoTest.Commits.First( sc => sc.Sha == "9af8722d001ebad998991c6779707ec4e406a822" );  // | | |\ origin/base-commit-nochange
    // Those 2 commits are unrelated but share the same content.
    static readonly SimpleCommit c2 = repoTest.Commits.First( sc => sc.Sha == "ef8dbe1e57a283daaddacf3894028305fa23a33d" ); // origin/unrelated-but-same-content-2
    static readonly SimpleCommit c1 = repoTest.Commits.First( sc => sc.Sha == "8cb516c6c4d384931b7542bd2404023befc23abd" ); // origin/unrelated-but-same-content-1

    [Test]
    public void when_the_head_is_above_the_tagged_version_Depth_is_positive_then_AlreadyExisting_error()
    {
        var overrides = new TagsOverride();
        {
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = overrides.Add( cBase.Sha, "1.0.0" ).Overrides,
                HeadBranchName = "beta",
                Branches = { new RepositoryInfoOptionsBranch( "beta", CIBranchVersionMode.LastReleaseBased ) }
            } );
            i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.AlreadyExistingVersion );
        }
    }

    [TestCase( "1.0.0", "1.0.1--0000-good" )]
    //[TestCase( "1.0.0-rc", "1.0.0-r00-00-0000-good" )]
    [TestCase( "1.0.0-epsilon", CommitInfo.ErrorCodeStatus.AlreadyExistingVersion )]
    public void when_the_head_is_below_the_version_or_on_an_unrelated_commit_Depth_is_always_0_then_we_may_save_the_0000( string releasedVersion, object result )
    {
        var overrides = new TagsOverride();
        {
            // Just below.
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = overrides.Add( cBeta.Sha, releasedVersion ).Overrides,
                HeadBranchName = "base-commit-nochange",
                Branches = { new RepositoryInfoOptionsBranch( "base-commit-nochange", CIBranchVersionMode.LastReleaseBased, "good" ) }
            } );
            CheckResult( i, result );
        }
        {
            // One level below.
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = overrides.Add( cGamma.Sha, releasedVersion ).Overrides,
                HeadBranchName = "base-commit-nochange",
                Branches = { new RepositoryInfoOptionsBranch( "base-commit-nochange", CIBranchVersionMode.LastReleaseBased, "good" ) }
            } );
            CheckResult( i, result );
        }
        {
            // Two levels below.
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = overrides.Add( cTop.Sha, releasedVersion ).Overrides,
                HeadBranchName = "base-commit-nochange",
                Branches = { new RepositoryInfoOptionsBranch( "base-commit-nochange", CIBranchVersionMode.LastReleaseBased, "good" ) }
            } );
            CheckResult( i, result );
        }
        {
            // When commits are unrelated but share the same content.
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = overrides.Add( c1.Sha, releasedVersion ).Overrides,
                HeadBranchName = "unrelated-but-same-content-2",
                Branches = { new RepositoryInfoOptionsBranch( "unrelated-but-same-content-2", CIBranchVersionMode.LastReleaseBased, "good" ) }
            } );
            CheckResult( i, result );
        }
    }

    static void CheckResult( CommitInfo i, object result )
    {
        if( result is string r ) i.FinalVersion.ToString().Should().Be( r );
        else i.ErrorCode.Should().Be( (CommitInfo.ErrorCodeStatus)result );
    }

    [TestCase( "DefaultBuildConfigurationSelector", "1.0.1--0000-good" )]
    [TestCase( "AlwaysRelease", CommitInfo.ErrorCodeStatus.AlreadyExistingVersion )]
    [TestCase( "AlwaysOther", CommitInfo.ErrorCodeStatus.AlreadyExistingVersion )]
    public void saving_or_not_the_0000_version_depends_on_the_BuildConfiguration( string selector, object result )
    {
        static string AlwaysRelease( in CommitInfo.InitialInfo commitInfo, SVersion v ) => "Release";

        static string AlwaysOther( in CommitInfo.InitialInfo commitInfo, SVersion v ) => "Other";

        if( selector == "AlwaysRelease" ) CommitInfo.BuildConfigurationSelector = AlwaysRelease;
        if( selector == "AlwaysOther" ) CommitInfo.BuildConfigurationSelector = AlwaysOther;
        try
        {
            CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
            {
                OverriddenTags = new TagsOverride().MutableAdd( cBeta.Sha, "1.0.0" ).Overrides,
                HeadBranchName = "base-commit-nochange",
                Branches = { new RepositoryInfoOptionsBranch( "base-commit-nochange", CIBranchVersionMode.LastReleaseBased, "good" ) }
            } );
            CheckResult( i, result );
        }
        finally
        {
            CommitInfo.BuildConfigurationSelector = CommitInfo.DefaultBuildConfigurationSelector;
        }

    }
}
