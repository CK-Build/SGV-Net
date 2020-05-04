using CSemVer;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SimpleGitVersion.Core.Tests
{
    [TestFixture]
    public class CheckExistingVersionTests
    {
        [Test]
        public void CheckExistingVersions_has_no_problem_when_there_is_no_version_at_all()
        {
            var repoTest = TestHelper.TestGitRepository;
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "parallel-world",
                    CheckExistingVersions = true
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption );
                i.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
            }
        }

        [Test]
        public void CheckExistingVersions_expects_at_least_one_existing_version_to_be_one_of_the_FirstPossibleVersions()
        {
            var repoTest = TestHelper.TestGitRepository;
            // high is also "origin/fumble-develop" branch.
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride();

            // Missing.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( high.Sha, "2.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CheckExistingVersionFirstMissing );
            }
            foreach( var oneFirst in CSVersion.FirstPossibleVersions )
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "origin/fumble-develop",
                    OverriddenTags = overrides.Add( high.Sha, oneFirst.ToString() ).Overrides,
                    CheckExistingVersions = true
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.None );
                i.FinalVersion.Should().Be( oneFirst );
            }
        }

        [Test]
        public void CheckExistingVersions_expects_the_StartingVersion_it_it_is_specified()
        {
            var repoTest = TestHelper.TestGitRepository;
            // high is also "origin/fumble-develop" branch.
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var medium = repoTest.Commits.Single( c => c.Message.StartsWith( "B-Commit." ) );
            var overrides = new TagsOverride();

            // Missing.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingVersion = "1.5.0",
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( high.Sha, "2.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CheckExistingVersionStartingVersionNotFound );
            }
            // With the StartingVersion.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingVersion = "1.5.0",
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( medium.Sha, "1.5.0" ).Add( high.Sha, "2.0.0" ).Overrides,
                    HeadCommit = high.Sha
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.None );
                i.FinalVersion.ToString().Should().Be( "2.0.0" );
            }
        }

        [Test]
        public void CheckExistingVersions_just_check_that_all_existing_versions_have_no_holes()
        {
            var repoTest = TestHelper.TestGitRepository;
            // high is also "origin/fumble-develop" branch.
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var medium = repoTest.Commits.Single( c => c.Message.StartsWith( "B-Commit." ) );
            var low = repoTest.Commits.Single( c => c.Message.StartsWith( "D-Commit." ) );
            var overrides = new TagsOverride();
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( low.Sha, "2.0.0" ).Add( medium.Sha, "1.1.0" ).Add( high.Sha, "1.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().NotBe( CommitInfo.ErrorCodeStatus.CheckExistingVersionHoleFound, "There's no hole here (even if they are not in the right order!)." );
            }
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( low.Sha, "2.0.0" ).Add( medium.Sha, "1.5.0" ).Add( high.Sha, "1.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CheckExistingVersionHoleFound, "Here, somethinh is missing between 1.5.0 and 1.0.0." );
            }
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    CheckExistingVersions = true,
                    OverriddenTags = overrides.Add( low.Sha, "4.0.0" ).Add( medium.Sha, "3.0.0" ).Add( high.Sha, "1.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CheckExistingVersionHoleFound, "Here, somethinh is missing between 3.0.0 and 1.0.0." );
            }
        }

    }
}
