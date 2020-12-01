using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Schema;
using NUnit.Framework;
using CSemVer;
using FluentAssertions;

namespace SimpleGitVersion.Core.Tests
{
    [TestFixture]
    public class RepositoryInfoTests
    {
        [Test]
        public void on_a_non_tagged_repository_all_commits_can_be_a_first_possible_version()
        {
            var repoTest = TestHelper.TestGitRepository;
            foreach( SimpleCommit c in repoTest.Commits )
            {
                CommitInfo i = repoTest.GetRepositoryInfo( c.Sha );
                (i.ErrorCode == CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached
                 || i.ErrorCode == CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption).Should().BeTrue();
                i.ReleaseTag.Should().BeNull();
                i.DetailedCommitInfo.BasicInfo.Should().BeNull();
                i.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.NextPossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.IsShallowCloned.Should().BeFalse();
            }
        }

        [Test]
        public void multiple_tags_on_the_same_commit_is_an_error()
        {
            var repoTest = TestHelper.TestGitRepository;
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride();
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = high.Sha,
                    OverriddenTags = overrides.Add( high.Sha, "1.0.0" ).Add( high.Sha, "2.0.0" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.MultipleVersionTagConflict );
                i.IsShallowCloned.Should().BeFalse();
            }
        }

        [Test]
        public void multiple_tags_on_the_same_commit_must_use_invalid_marker()
        {
            var repoTest = TestHelper.TestGitRepository;
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride();
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = high.Sha,
                    OverriddenTags = overrides.Add( high.Sha, "1.0.0+invalid" ).Add( high.Sha, "1.0.0-beta" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.None );
                i.FinalVersion.ToString().Should().Be( "1.0.0-b" );
            }
        }

        [Test]
        public void multiple_tags_on_the_same_commit_is_an_error_even_if_there_are_also_invalid_markers()
        {
            var repoTest = TestHelper.TestGitRepository;
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride();
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = high.Sha,
                    OverriddenTags = overrides.Add( high.Sha, "1.0.0" ).Add( high.Sha, "2.0.0" ).Add( high.Sha, "1.1.1+Invalid" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.MultipleVersionTagConflict );
            }
        }
        [Test]
        public void only_invalid_markers_are_ignored()
        {
            var repoTest = TestHelper.TestGitRepository;
            var high = repoTest.Commits.Single( c => c.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride();
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = high.Sha,
                    OverriddenTags = overrides.Add( high.Sha, "1.0.0+Invalid" ).Add( high.Sha, "2.0.0+Invalid" ).Add( high.Sha, "1.1.1+Invalid" ).Overrides,
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption );
                i.IsShallowCloned.Should().BeFalse();
            }
        }

        [Test]
        public void repository_with_the_very_first_version_only()
        {
            var repoTest = TestHelper.TestGitRepository;
            var tagged = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second b/b1" ) );

            var bb1Tag = CSVersion.VeryFirstVersion;
            var overrides = new TagsOverride().MutableAdd( tagged.Sha, bb1Tag.ToString() );

            Action<SimpleCommit> checkOK = sc =>
            {
                var i = repoTest.GetRepositoryInfo( sc.Sha, overrides );

                (i.ErrorCode == CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached
                 || i.ErrorCode == CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption).Should().BeTrue();

                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEquivalentTo( bb1Tag.GetDirectSuccessors() );
                // Now tag the commit and checks that each tag is valid.
                foreach( var next in bb1Tag.GetDirectSuccessors() )
                {
                    var iWithTag = repoTest.GetRepositoryInfo( sc.Sha, overrides.Add( sc.Sha, next.ToString() ) );
                    Assert.That( iWithTag.FinalVersion, Is.EqualTo( next ) );
                }
                i.IsShallowCloned.Should().BeFalse();
            };

            Action<SimpleCommit> checkKO = sc =>
            {
                var i = repoTest.GetRepositoryInfo( sc.Sha, overrides );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty();
                // Now tag the commit and checks that each tag is invalid.
                foreach( var next in bb1Tag.GetDirectSuccessors() )
                {
                    var iWithTag = repoTest.GetRepositoryInfo( sc.Sha, overrides.Add( sc.Sha, next.ToString() ) );
                    iWithTag.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.ReleaseTagIsNotPossible );
                    iWithTag.FinalVersion.Should().Be( SVersion.ZeroVersion );
                }
            };

            // The version on the commit point.
            {
                var i = repoTest.GetRepositoryInfo( tagged.Sha, overrides );
                i.FinalVersion.ToString().Should().Be( "0.0.0-a" );
                i.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
            };

            // Checking possible versions before: none.
            var before1 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'a' into b" ) );
            checkKO( before1 );
            var before2 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second a/a2" ) );
            checkKO( before2 );
            var before3 = repoTest.Commits.First( sc => sc.Message.StartsWith( "On master again" ) );
            checkKO( before3 );

            // Checking possible versions after: all successors are allowed.
            var after1 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second b/b2" ) );
            checkOK( after1 );
            var after2 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'b' into c" ) );
            checkOK( after2 );
            var after3 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branches 'c', 'd' and 'e'" ) );
            checkOK( after3 );

        }

        [Test]
        public void ignoring_legacy_versions_with_StartingVersion_option()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cOK = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second b/b1" ) );
            var cKO1 = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second a/a1" ) );
            var cKO2 = repoTest.Commits.First( sc => sc.Message.StartsWith( "First b/b1" ) );
            var cKO3 = repoTest.Commits.First( sc => sc.Message.StartsWith( "First a/a2" ) );

            var overrides = new TagsOverride()
                                    .MutableAdd( cOK.Sha, "4.0.3-beta" )
                                    .MutableAdd( cKO1.Sha, "0.0.0-alpha" )
                                    .MutableAdd( cKO2.Sha, "1.1.0" )
                                    .MutableAdd( cKO3.Sha, "2.0.2" );

            {
                CommitInfo i = repoTest.GetRepositoryInfo( cOK.Sha, overrides );
                i.Error.Should().Match( "Release tag '*' is not valid here.*" );
            }
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cOK.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersion = "4.0.3-beta"
                } );
                i.Error.Should().BeNull();
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.None );
                i.ReleaseTag.ToString().Should().Be( "4.0.3-b" );
                i.FinalVersion.Should().BeSameAs( i.ReleaseTag );
                i.PossibleVersions.Select( t => t.ToString() ).Should().BeEquivalentTo( new[] { "4.0.3-b" } );
            }
            {
                var cAbove = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second b/b2" ) );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cAbove.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersion = "4.0.3-beta"
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached );
                Assert.That( i.DetailedCommitInfo.BasicInfo.BestCommitBelow.ThisTag.ToString(), Is.EqualTo( "4.0.3-b" ) );
                i.Error.Should().NotBeNull();
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                CollectionAssert.Contains( i.PossibleVersions.Select( t => t.ToString() ), "4.0.3-b00-01", "4.0.3-b01", "4.0.3-d", "4.0.3", "4.1.0-r", "4.1.0", "5.0.0" );
            }

            // Commit before the StartingVersion has no PossibleVersions.
            {
                var cBelow = repoTest.Commits.First( sc => sc.Message.StartsWith( "On master again" ) );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cBelow.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersion = "4.0.3-beta"
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached );
                i.DetailedCommitInfo.BasicInfo.Should().BeNull();
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty();
            }
            {
                var cBelow = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'a' into b" ) );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cBelow.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersion = "4.0.3-beta"
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached );
                i.DetailedCommitInfo.BasicInfo.Should().BeNull();
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty();
            }
        }

        [Test]
        public void AlreadyExistingVersion_demo()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cAlpha = repoTest.Commits.First( sc => sc.Message.StartsWith( "Real Dev in Alpha." ) );
            // cReleased is "Merge branch 'gamma' into parallel-world" but there are two of them...
            // This is the head of parallel-world branch.
            var cReleased = repoTest.Commits.First( sc => sc.Sha == "fc9802013c23398978744de1618fb01638f7347e" );
            var v1beta = CSVersion.TryParse( "1.0.0-beta" );
            var overrides = new TagsOverride().MutableAdd( cAlpha.Sha, "1.0.0-beta" );

            // cReleased       ==> This locates the "parallel-world" branch.
            //   |
            //   |
            // cAlpha - v1.0.0-beta

            // cReleased has already been released by its 1.0.0-beta parent.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption );
                i.StartingCommit.ConsideredBranchNames.Should().BeEquivalentTo( "parallel-world" );
                i.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.AlreadyExistingVersion.ThisTag.Should().Be( v1beta );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty( "Since there is a AlreadyExistingVersion." );
            }
            // Trying to release it is an error because of the AlreadyExistingVersion.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Add( cReleased.Sha, "v1.0.0-rc" ).Overrides
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.AlreadyExistingVersion );
                i.Error.Should().StartWith( "This commit has already been released with version '1.0.0-b', by commit '" );
                i.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.AlreadyExistingVersion.ThisTag.Should().Be( v1beta );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty( "Since there is a AlreadyExistingVersion." );
            }

            // Same but with IgnoreAlreadyExistingVersion = true.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no CI Branch information defined for branch 'parallel-world'." );
                i.AlreadyExistingVersion.CommitSha.Should().Be( cAlpha.Sha );
                i.DetailedCommitInfo.BasicInfo.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.ReleaseTag.Should().BeNull();
                i.PossibleVersions.Should().BeEquivalentTo( v1beta.GetDirectSuccessors(), "The possible versions are not reset by the existing AlreadyExistingVersion." );
            }
            // Releasing it is possible.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Add( cReleased.Sha, "v1.0.0-rc" ).Overrides
                } );
                i.Error.Should().BeNull();
                i.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.AlreadyExistingVersion.ThisTag.Should().Be( v1beta );
                i.FinalVersion.ToString().Should().Be( "1.0.0-r" );
                i.PossibleVersions.Should().BeEquivalentTo( v1beta.GetDirectSuccessors(), "The possible versions are not reset by the existing AlreadyExistingVersion." );
            }
        }


        [Test]
        public void propagation_through_multiple_hops()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cAlpha = repoTest.Commits.First( sc => sc.Message.StartsWith( "Real Dev in Alpha." ) );
            // cReleased is "Merge branch 'gamma' into parallel-world" but there are two of them...
            // This is the head of parallel-world branch.
            var cReleased = repoTest.Commits.First( sc => sc.Sha == "fc9802013c23398978744de1618fb01638f7347e" );
            var v1beta = CSVersion.TryParse( "1.0.0-beta" );
            var overrides = new TagsOverride().MutableAdd( cAlpha.Sha, "1.0.0-beta" );

            // cReleased
            //   |
            //   |
            // cAlpha - v1.0.0-beta

            // This is "normal": cReleased has 1.0.0-beta in its parent.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no CI Branch information defined for branch 'parallel-world'." );
                i.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.AlreadyExistingVersion.ThisTag.Should().Be( v1beta );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEmpty( "Since there is a AlreadyExistingVersion." );
            }
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cReleased.Sha,
                    IgnoreAlreadyExistingVersion = true,
                    OverriddenTags = overrides.Overrides
                } );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no CI Branch information defined for branch 'parallel-world'." );
                i.BestCommitBelow.ThisTag.Should().Be( v1beta );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
                i.PossibleVersions.Should().BeEquivalentTo( v1beta.GetDirectSuccessors() );
            }

            var cAlphaContinue = repoTest.Commits.First( sc => sc.Message.StartsWith( "Dev again in Alpha." ) );
            // We set 2.0.0 on cReleased. Its content is the same as cAlpha (mege commits with no changes).
            // To be able to do this we NEED to use the StartingVersion
            //
            // cAlphaContinue
            //   |
            //   |    cReleased - v2.0.0
            //   |  /
            //   |/
            // cAlpha - v1.0.0-beta

            overrides.MutableAdd( cReleased.Sha, "2.0.0" );
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.Error.Should().Contain( "is not valid here." );
                i.ReleaseTag.ToString().Should().Be( "2.0.0" );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
            }
            // Use the StartingVersion:
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingVersion = "2.0.0",
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.ToString().Should().Be( "2.0.0" );
                i.FinalVersion.ToString().Should().Be( "2.0.0" );
            }
            // Using IgnoreAlreadyExistingVersion is not enough: 1.0.0-b cannot be followed by 2.0.0.
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    HeadCommit = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.ReleaseTagIsNotPossible );
                i.Error.Should().StartWith( "Release tag '2.0.0' is not valid here." );
                i.ReleaseTag.ToString().Should().Be( "2.0.0" );
                i.FinalVersion.Should().Be( SVersion.ZeroVersion );
            }
            // Subsequent developments of alpha branch now starts after 2.0.0, for instance 2.1.0-beta.
            overrides.MutableAdd( cAlphaContinue.Sha, "2.1.0-beta" );
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cAlphaContinue.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                var tagged = CSVersion.TryParse( "2.1.0-b" );
                i.Error.Should().BeNull();
                i.FinalVersion.Should().Be( tagged );
                // alpha branch can continue with any successors v2.0.0.
                i.PossibleVersions.Should().BeEquivalentTo( CSVersion.TryParse( "2.0.0" ).GetDirectSuccessors() );
            }
        }

        [Test]
        public void CIBuildVersion_with_merged_tags()
        {
            var repoTest = TestHelper.TestGitRepository;

            var cRoot = repoTest.Commits.Single( sc => sc.Message.StartsWith( "First in parallel world." ) );
            var cDevInAlpha = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Dev in Alpha." ) );
            var cDevInBeta = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Dev in Beta." ) );
            var cDevInGamma = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Dev in Gamma." ) );

            var overrides = new TagsOverride()
                        .MutableAdd( cRoot.Sha, "v1.0.0" )
                        .MutableAdd( cDevInAlpha.Sha, "v2.0.0" );

            // cDevInBeta
            //   |
            //   |  cDevInGamma
            //   | / 
            //   |/   cDevInAlpha - v2.0.0
            //   |   /
            //   |  /
            //   | /
            // cRoot - v1.0.0

            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.FinalVersion.Should().Be( CSVersion.TryParse( "v2.0.0" ) );
            }

            overrides.MutableAdd( cDevInBeta.Sha, "v1.0.1-beta" );
            // cDevInBeta - v1.0.1-beta
            //   |
            //   |  cDevInGamma
            //   | / 
            //   |/   cDevInAlpha - v2.0.0
            //   |  /
            //   | /
            // cRoot - v1.0.0
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cDevInBeta.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.FinalVersion.Should().Be( CSVersion.TryParse( "v1.0.1-b" ) );
            }

            overrides.MutableAdd( cDevInGamma.Sha, "v1.0.1-alpha" );
            // cDevInBeta - v1.0.1-beta
            //   |
            //   |  cDevInGamma - v1.0.1-alpha
            //   | / 
            //   |/   cDevInAlpha - v2.0.0
            //   |  /
            //   | /
            // cRoot - v1.0.0
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cDevInGamma.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.FinalVersion.Should().Be( CSVersion.TryParse( "v1.0.1-a" ) );
            }
            // On "gamma" branch, the head is 7 commits ahead of the v2.0.0 tag: this is the longest path. 
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "gamma",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "gamma", CIBranchVersionMode.LastReleaseBased )
                    }
                } );
                i.Error.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "2.0.1--0007-gamma" );
            }
            // Testing "gamma" branch in ZeroTimed mode. 
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "gamma",
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "gamma", CIBranchVersionMode.ZeroTimed )
                    }
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "0.0.0--009y09h-gamma+v2.0.0" );
            }
            // On "alpha" branch, the head is 6 commits ahead of the v2.0.0 tag (always the take the longest path). 
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "alpha",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.LastReleaseBased, "ALPHAAAA" )
                    }
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "2.0.1--0006-ALPHAAAA" );
            }
            // Testing "alpha" branch in ZeroTimed mode.  
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "alpha",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.ZeroTimed, "ALPH" )
                    }
                } );
                i.Error.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "0.0.0--009y6hm-ALPH+v2.0.0" );
            }
            // On "beta" branch, the head is 6 commits ahead of the v2.0.0 tag. 
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "beta",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "beta", CIBranchVersionMode.LastReleaseBased, "BBBBBB" )
                    }
                } );
                i.Error.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "2.0.1--0006-BBBBBB" );
            }
            // Testing ZeroTimed mode on "beta" branch. 
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = "beta",
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "beta", CIBranchVersionMode.ZeroTimed, "beta" )
                    }
                } );
                i.Error.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( "0.0.0--009y087-beta+v2.0.0" );
            }

        }

        [TestCase( "v1.0.0", "alpha", null, "1.0.1--0006-alpha" )]
        [TestCase( "v1.0.0", "beta", null, "1.0.1--0006-beta" )]
        [TestCase( "v1.0.0", "parallel-world", "parallel", "1.0.1--0008-parallel" )]
        [TestCase( "v0.1.0-beta", "alpha", null, "0.1.0-b00-00-0006-alpha" )]
        [TestCase( "v0.0.0-rc", "beta", null, "0.0.0-r00-00-0006-beta" )]
        public void CIBuildVersion_from_DevInAlpha_commits_ahead_tests( string vOnDevInAlpha, string branchName, string branchVersionName, string ciBuildVersion )
        {
            var repoTest = TestHelper.TestGitRepository;
            var cDevInAlpha = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Dev in Alpha." ) );
            var overrides = new TagsOverride().MutableAdd( cDevInAlpha.Sha, vOnDevInAlpha );
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = branchName,
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( branchName, CIBranchVersionMode.LastReleaseBased, branchVersionName )
                    }
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.ToString().Should().Be( ciBuildVersion );
            }
        }

        [TestCase( "v0.0.0-alpha.1.1", "alpha", null, "0.0.0-a01-01-0006-alpha" )]
        [TestCase( "v0.0.0-alpha.2", "alpha", null, "0.0.0-a02-00-0006-alpha" )]
        [TestCase( "v0.0.0-beta", "alpha", null, "0.0.0-b00-00-0006-alpha" )]

        [TestCase( "v0.0.0-alpha.1.1", "beta", null, "0.0.0-a01-01-0006-beta" )]
        [TestCase( "v0.0.0-alpha.2", "beta", null, "0.0.0-a02-00-0006-beta" )]
        [TestCase( "v0.0.0-beta", "beta", null, "0.0.0-b00-00-0006-beta" )]

        [TestCase( "v0.0.0-alpha.1.1", "parallel-world", "parallel", "0.0.0-a01-01-0008-parallel" )]
        [TestCase( "v0.0.0-alpha.2", "parallel-world", "parallel", "0.0.0-a02-00-0008-parallel" )]
        [TestCase( "v0.0.0-beta", "parallel-world", "parallel", "0.0.0-b00-00-0008-parallel" )]

        [TestCase( "v0.0.0-nimp", "f-beta-nothing", "XXX", "0.0.0-a01-00-0004-XXX" )]
        [TestCase( "v0.0.0-dont-care", "f-beta-nothing", "YYYY", "0.0.0-a01-00-0004-YYYY" )]
        [TestCase( "v0.0.0-onDevInAlpha", "f-beta-nothing", "B", "0.0.0-a01-00-0004-B" )]
        public void CIBuildVersion_from_DevInAlpha_commits_ahead( string vDevInAlpha, string branchName, string branchNameVersion, string ciBuildVersion )
        {
            var repoTest = TestHelper.TestGitRepository;
            var cRoot = repoTest.Commits.First( sc => sc.Message.StartsWith( "First in parallel world." ) );
            var cPickChange = repoTest.Commits.First( sc => sc.Message.StartsWith( "Cherry Pick - Change in parallel-world.txt content (1)." ) );
            var cDevInAlpha = repoTest.Commits.First( sc => sc.Message.StartsWith( "Dev in Alpha." ) );

            //
            // branch: alpha, beta, parallel-world
            //                  |
            //                 ~~~
            //                  |
            // cDevInAlpha      + "vDevInAlpha"
            //                  |        
            //                  |       
            // branch:          |     f-beta-nothing       
            //                  |     /
            //                  |    /
            //                  |   /
            //                  |  /
            //                  | /
            //                  |/
            // cPickChange      +   v0.0.0-alpha.1
            //                  |
            //                  |
            // cRoot            +   v0.0.0-alpha

            var overrides = new TagsOverride()
                .MutableAdd( cRoot.Sha, "v0.0.0-alpha" )
                .MutableAdd( cPickChange.Sha, "v0.0.0-alpha.1" )
                .MutableAdd( cDevInAlpha.Sha, vDevInAlpha );

            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadBranchName = branchName,
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( branchName, CIBranchVersionMode.LastReleaseBased, branchNameVersion )
                    }
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.Should().BeNull();
                i.FinalVersion.Should().BeSameAs( i.CIRelease.BuildVersion );
                i.FinalVersion.NormalizedText.Should().Be( ciBuildVersion );
            }
        }


        [Test]
        public void options_can_contain_IgnoreModified_files()
        {
            var repoTest = TestHelper.TestGitRepository;
            string fileToChange = Directory.EnumerateFiles( repoTest.Path ).FirstOrDefault();
            Assume.That( fileToChange, Is.Not.Null );

            Console.WriteLine( "Modifiying '{0}'.", fileToChange );

            byte[] original = File.ReadAllBytes( fileToChange );

            try
            {
                var options = new RepositoryInfoOptions();

                CommitInfo info;

                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False );

                File.WriteAllText( fileToChange, "!MODIFIED!" );
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.True );

                options.IgnoreModifiedFiles.Add( fileToChange.Substring( repoTest.Path.Length + 1 ) );
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False );
            }
            finally
            {
                File.WriteAllBytes( fileToChange, original );
            }
        }


        [Test]
        public void options_IgnoreModified_files_filter()
        {
            var repoTest = TestHelper.TestGitRepository;
            repoTest.CheckOut( "origin/parallel-world" );

            string devPath = Path.Combine( repoTest.Path, "Dev in Alpha.txt" );
            string devTxt = File.ReadAllText( devPath );
            Assume.That( devTxt, Is.EqualTo( "Dev in Aplpha." ) );

            string realDevPath = Path.Combine( repoTest.Path, "Real Dev in Alpha.txt" ); ;
            string realDevTxt = File.ReadAllText( realDevPath );
            Assume.That( realDevTxt, Is.EqualTo( "Real Dev in Alpha." ) );

            try
            {
                CommitInfo info;
                var options = new RepositoryInfoOptions();
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False, "Working folder is clean." );

                File.WriteAllText( devPath, "!MODIFIED!" + devTxt );
                File.WriteAllText( realDevPath, "!MODIFIED!" + realDevTxt );

                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, "Working folder is dirty." );

                options.IgnoreModifiedFiles.Add( "Dev in Alpha.txt" );
                options.IgnoreModifiedFiles.Add( "Real Dev in Alpha.txt" );
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False, "Working folder is dirty but IgnoreModifiedFiles explicitly ignores the 2 files." );

                int nbCall = 0;
                options.IgnoreModifiedFiles.Clear();
                options.IgnoreModifiedFilePredicate = m =>
                {
                    // Always returns true: the file is NOT modified.
                    ++nbCall;
                    return true;
                };
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False, "Working folder is dirty but IgnoreModifiedFilePredicate explicitly ignores all files." );
                Assert.That( nbCall, Is.EqualTo( 2 ) );

                nbCall = 0;
                options.IgnoreModifiedFilePredicate = m =>
                {
                    // Returns false: the file is actually modified.
                    // without IgnoreModifiedFileFullProcess, this stops the lookups.
                    ++nbCall;
                    return false;
                };
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, "Working folder is dirty (IgnoreModifiedFilePredicate returned false)." );
                Assert.That( nbCall, Is.EqualTo( 1 ), "As soon as the predicate returns false, the lookup stops." );

                nbCall = 0;
                options.IgnoreModifiedFileFullProcess = true;
                options.IgnoreModifiedFilePredicate = m =>
                {
                    // Returns false: the file is actually modified.
                    // with IgnoreModifiedFileFullProcess = true, the process continues.
                    ++nbCall;
                    return false;
                };
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, "Working folder is dirty (IgnoreModifiedFilePredicate returned false)." );
                Assert.That( nbCall, Is.EqualTo( 2 ), "Thanks to IgnoreModifiedFileFullProcess, all modified files are processed." );

                nbCall = 0;
                options.IgnoreModifiedFiles.Add( "Dev in Alpha.txt" );
                options.IgnoreModifiedFilePredicate = m =>
                {
                    ++nbCall;
                    Assert.That( m.Path, Is.Not.EqualTo( "Dev in Alpha.txt" ), "This has been filtered by IgnoreModifiedFiles set." );
                    Assert.That( m.CommittedText, Is.EqualTo( "Real Dev in Alpha." ) );
                    return m.Path == "Real Dev in Alpha.txt";
                };
                info = repoTest.GetRepositoryInfo( options );
                Assert.That( info.IsDirty, Is.False, "Working folder is dirty but IgnoreModifiedFiles ignores one file and ModifiedFileFilter ignores the other one." );
                Assert.That( nbCall, Is.EqualTo( 1 ) );
            }
            finally
            {
                File.WriteAllText( devPath, devTxt );
                File.WriteAllText( realDevPath, realDevTxt );
            }
        }

        [Test]
        public void fumble_commit_scenario()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cD = repoTest.Commits.Single( sc => sc.Message.StartsWith( "D-Commit." ) );
            var cC = repoTest.Commits.Single( sc => sc.Message.StartsWith( "C-Commit." ) );
            var cF = repoTest.Commits.Single( sc => sc.Sha == "27a629754c6b9034f7ca580442b589a0241773c5" );
            var cB = repoTest.Commits.Single( sc => sc.Message.StartsWith( "B-Commit." ) );
            var cA = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Merge branch 'fumble-develop' into fumble-master" ) );
            var cFix = repoTest.Commits.Single( sc => sc.Sha == "e6766d127f9a2df42567151222c6569601614626" );
            var cX = repoTest.Commits.Single( sc => sc.Message.StartsWith( "X-Commit." ) );
            var overrides = new TagsOverride()
                .MutableAdd( cD.Sha, "v4.3.2" )
                .MutableAdd( cC.Sha, "v4.4.0-alpha" )
                .MutableAdd( cB.Sha, "v5.0.0-rc" )
                .MutableAdd( cA.Sha, "v5.0.0" );
            var v5 = CSVersion.TryParse( "v5.0.0" );
            var v5rc = CSVersion.TryParse( "v5.0.0-rc" );
            var v5rc01 = CSVersion.TryParse( "v5.0.0-rc.0.1" );
            var v5rc1 = CSVersion.TryParse( "v5.0.0-rc.1" );

            // cX     +   
            //        |    
            // cFix   +         This commit point has the same content as cM (the "master").
            //        |         => Publishing from this commit is possible only if IgnoreAlreadyExistingVersion is set to true.
            //        |            
            //        |    
            // cM     |    +     v5.0.0 - Merge in "master"
            //        |   /|
            //        |  / |
            //        | /  |
            //        |/   |
            // cB     +    |     v5.0.0-rc
            //        |    |
            //        |    |
            // cC     +    |     v4.4.0-alpha
            //        |    |    
            // cF     |    +     Fumble Commit (commit in "master" that should have been done on "dev").     
            //        |   /          
            //        |  /          
            //        | /          
            //        |/          
            // cD     +          v4.3.2

            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cFix.Sha
                } );
                // On the fumble commit, no release is possible.
                // Above the fumble commit, any successor of the 5.0.0 is possible.
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildHeadCommitIsDetached );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no branches reference the specified commit 'e6766d127f9a2df42567151222c6569601614626'." );
                i.AlreadyExistingVersion.ThisTag.Should().Be( v5 );
                i.PossibleVersions.Should().BeEmpty();
                i.NextPossibleVersions.Should().BeEquivalentTo( v5.GetDirectSuccessors() );
            }
            {
                // Above the fix of the fumble commit, any successor of the 5.0.0 is possible.
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cX.Sha
                } );
                i.ErrorCode.Should().Be( CommitInfo.ErrorCodeStatus.CIBuildMissingBranchOption );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no CI Branch information defined for branch 'fumble-develop'." );
                i.AlreadyExistingVersion.Should().BeNull();
                i.PossibleVersions.Should().BeEquivalentTo( v5.GetDirectSuccessors() );
                i.NextPossibleVersions.Should().BeEquivalentTo( v5.GetDirectSuccessors() );
            }
        }

        [Test]
        public void fumble_commit_plus_an_extra_content_with_a_big_release_number()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cD = repoTest.Commits.First( sc => sc.Message.StartsWith( "D-Commit." ) );
            var cC = repoTest.Commits.First( sc => sc.Message.StartsWith( "C-Commit." ) );
            var cF = repoTest.Commits.First( sc => sc.Sha == "27a629754c6b9034f7ca580442b589a0241773c5" );
            var cB = repoTest.Commits.First( sc => sc.Message.StartsWith( "B-Commit." ) );
            var cM = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'fumble-develop' into fumble-master" ) );
            var cFix = repoTest.Commits.First( sc => sc.Sha == "e6766d127f9a2df42567151222c6569601614626" );
            var cX = repoTest.Commits.First( sc => sc.Message.StartsWith( "X-Commit." ) );
            var cExtra = repoTest.Commits.First( sc => sc.Message.StartsWith( "C-Commit (cherry pick)." ) );

            // cExtra  +            v10.0.0 - This has the same content (cherry pick) as cC (here in v4.4.0-alpha). 
            //         |      
            //         |       
            //         |       
            // cFix    |   +         This commit point has the same content as cM (the "master"): IgnoreAlreadyExistingVersion must be set to true!
            //         |   |         => Without SingleMajor = 5, v10 wins. 
            //         |   |    
            // cM      |   |    +     v5.0.0 - Merge in "master"
            //         |   |   /|
            //         |   |  / |
            //         |   | /  |
            //         |   |/   |
            // cB      |   +    |     v5.0.0-rc
            //         |   |    |
            //         |   |    |
            // cC      |   +    |     v4.4.0-alpha (but now its content is actually v10.0.0)
            //         |   |    |    
            // cF      |   |    +     Fumble Commit (commit in "master" that should have been done on "dev").     
            //         \   |   /          
            //          \  |  /          
            //           \ | /          
            //            \|/          
            // cD          +          v4.3.2


            var overrides = new TagsOverride()
                .MutableAdd( cD.Sha, "v4.3.2" )
                .MutableAdd( cC.Sha, "v4.4.0-alpha" )
                .MutableAdd( cB.Sha, "v5.0.0-rc" )
                .MutableAdd( cM.Sha, "v5.0.0" )
                .MutableAdd( cExtra.Sha, "v10.0.0" );
            var v5 = CSVersion.TryParse( "v5.0.0" );
            var v5rc = CSVersion.TryParse( "v5.0.0-rc" );
            var v5rc01 = CSVersion.TryParse( "v5.0.0-rc.0.1" );
            var v5rc1 = CSVersion.TryParse( "v5.0.0-rc.1" );
            var v10 = CSVersion.TryParse( "v10.0.0" );
            {
                // On cFix, Without SingleMajor = 5, v10 wins. 
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cFix.Sha
                } );
                i.AlreadyExistingVersion.ThisTag.ToString().Should().Be( "5.0.0" );
                i.BestCommitBelow.ThisTag.ToString().Should().Be( "10.0.0" );
                i.PossibleVersions.Should().BeEquivalentTo( v10.GetDirectSuccessors() );
                
                CommitInfo iLTS = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cFix.Sha,
                    SingleMajor = 5
                } );
                var allowed = v5.GetDirectSuccessors().Where( v => v.Major == 5 );
                iLTS.PossibleVersions.Should().BeEquivalentTo( allowed );
            }
            {
                // Without SingleMajor = 5, On B-Commit (just above v4.4.0-alpha) v10 wins.
                var v44a = CSVersion.TryParse( "v4.4.0-alpha" );
                var v44a01 = CSVersion.TryParse( "v4.4.0-alpha.0.1" );
                var v44a1 = CSVersion.TryParse( "v4.4.0-alpha.1" );
                var v500 = CSVersion.TryParse( "v5.0.0" );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cB.Sha
                } );
                i.PossibleVersions.Should().BeEquivalentTo( v10.GetDirectSuccessors() );

                CommitInfo iLTS = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cB.Sha,
                    SingleMajor = 4
                } );
                var allowed = v44a.GetDirectSuccessors().Where( v => v.Major == 4 );
                iLTS.PossibleVersions.Should().BeEquivalentTo( allowed );
            }
            {
                // On C-Extra: the v10.0.0 is actually not allowed since it is not a
                // successor of v4.3.2.
                // The possible versions here are the successors of v4.4.0-alpha (up to the
                // existing v5.0.0-rc) because its content is tagged with v4.4.0-alpha.
                var v432 = CSVersion.TryParse( "v4.3.2" );
                var v44a = CSVersion.TryParse( "v4.4.0-alpha" );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    IgnoreAlreadyExistingVersion = true,
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cExtra.Sha
                } );
                i.Error.Should().StartWith( "Release tag '10.0.0' is not valid here." );
                i.AlreadyExistingVersion.ThisTag.ToString().Should().Be( "4.4.0-a" );
                i.PossibleVersions.Should().BeEquivalentTo( v44a.GetDirectSuccessors().Where( v => v < v5rc ) );
                // But the v10.0.0 tag exits, the versions above cExtra are
                i.NextPossibleVersions.Should().BeEquivalentTo( v10.GetDirectSuccessors() );
            }
        }

        [Test]
        public void multiple_version_tags_on_the_same_commit()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cRealDevInAlpha = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Real Dev in Alpha." ) );
            var overrides = new TagsOverride().MutableAdd( cRealDevInAlpha.Sha, "1.0.0" )
                                              .MutableAdd( cRealDevInAlpha.Sha, "2.0.0" );
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cRealDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides,
                } );
                i.ReleaseTag.Should().BeNull();
                i.Error.Trim().Should().Be( $"Commit '{cRealDevInAlpha.Sha}' has 2 different released version tags. Delete some of them or create +invalid tag(s) if they are already pushed to a remote repository." );
            }
            {
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    HeadCommit = cRealDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersion = "2.0.0"
                } );
                i.Error.Should().BeNull();
                i.ReleaseTag.Should().Be( CSVersion.Parse( "2.0.0" ) );
                i.FinalVersion.Should().BeSameAs( i.ReleaseTag );
            }
        }

        [Test]
        public void when_there_is_no_versions_below()
        {
            var repoTest = TestHelper.TestGitRepository;
            {
                var cOrphan = repoTest.Commits.Single( sc => sc.Message.StartsWith( "First in parallel world." ) );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions { HeadCommit = cOrphan.Sha } );
                i.DetailedCommitInfo.BasicInfo.Should().BeNull();
                i.DetailedCommitInfo.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.CIRelease.Should().BeNull();
            }
            {
                var options = new RepositoryInfoOptions
                {
                    HeadBranchName = "alpha",
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.ZeroTimed )
                    }
                };

                CommitInfo i = repoTest.GetRepositoryInfo( options );
                i.DetailedCommitInfo.BasicInfo.Should().BeNull();
                i.DetailedCommitInfo.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.CIRelease.Should().NotBeNull();
            }
        }

        [Test]
        public void simple_label_failure()
        {
            var repoTest = TestHelper.TestGitRepository;
            var cD = repoTest.Commits.Single( sc => sc.Message.StartsWith( "D-Commit." ) );
            var cF = repoTest.Commits.Single( sc => sc.Sha == "27a629754c6b9034f7ca580442b589a0241773c5" );
            var cC = repoTest.Commits.Single( sc => sc.Message.StartsWith( "C-Commit." ) );

            //        
            // cC   +          v4.2.1-rc
            //      |        
            // cF   |    +     v4.2.1-alpha     
            //      |   /          
            //      |  /          
            //      | /          
            //      |/          
            // cD   +          v4.2.0

            var console = new ConsoleLogger();

            var overrides = new TagsOverride()
                .MutableAdd( cD.Sha, "v4.2.0" )
                .MutableAdd( cF.Sha, "v4.2.1-alpha" )
                .MutableAdd( cC.Sha, "v4.2.1-rc" );
            {
                console.Info( "--------- rc/alpha conflict." );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cC.Sha
                } );
                i.Error.Should().StartWith( "Release tag '4.2.1-r' is not valid here." );
                i.Explain( console );
            }
            {
                console.Info( "--------- With v4.2.1-a+invalid" );
                overrides.MutableAdd( cF.Sha, "v4.2.1-a+invalid" );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cC.Sha
                } );
                i.Error.Should().BeNull();
                i.Explain( console );
            }
            {
                console.Info( "--------- With v4.2.1-a+invalid and no more v4.2.1-rc" );
                overrides.MutableRemove( cC.Sha, "v4.2.1-rc" );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cC.Sha
                } );
                i.Error.Should().StartWith( "No release tag found and CI build is not possible: no CI Branch information defined for branch 'branch-on-C-Commit'." );
                i.Explain( console );
            }
            {
                console.Info( "--------- Same as before but now the branch is configured for CI." );
                overrides.MutableRemove( cC.Sha, "v4.2.1-rc" );
                CommitInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    HeadCommit = cC.Sha,
                    Branches = { new RepositoryInfoOptionsBranch( "branch-on-C-Commit", CIBranchVersionMode.LastReleaseBased, "test" ) }
                } );
                i.Error.Should().BeNull();
                i.FinalVersion.ToString().Should().Be( "4.2.1--0001-test" );
                i.Explain( console );
            }
        }


    }
}
