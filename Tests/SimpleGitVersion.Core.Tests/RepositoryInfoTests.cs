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
                RepositoryInfo i = repoTest.GetRepositoryInfo( c.Sha );
                Assert.That( i.StartingCommitInfo.Error, Is.Null );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CommitInfo.BasicInfo, Is.Null );
                CollectionAssert.AreEqual( CSVersion.FirstPossibleVersions, i.PossibleVersions );
                CollectionAssert.AreEqual( CSVersion.FirstPossibleVersions, i.NextPossibleVersions );
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
                Assert.That( i.ValidReleaseTag, Is.Null );
                CollectionAssert.AreEqual( bb1Tag.GetDirectSuccessors(), i.PossibleVersions );
                // Now tag the commit and checks that each tag is valid.
                foreach( var next in bb1Tag.GetDirectSuccessors() )
                {
                    var iWithTag = repoTest.GetRepositoryInfo( sc.Sha, overrides.Add( sc.Sha, next.ToString() ) );
                    Assert.That( iWithTag.ValidReleaseTag, Is.EqualTo( next ) );
                }
            };

            Action<SimpleCommit> checkKO = sc =>
            {
                var i = repoTest.GetRepositoryInfo( sc.Sha, overrides );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.PossibleVersions, Is.Empty );
                // Now tag the commit and checks that each tag is invalid.
                foreach( var next in bb1Tag.GetDirectSuccessors() )
                {
                    var iWithTag = repoTest.GetRepositoryInfo( sc.Sha, overrides.Add( sc.Sha, next.ToString() ) );
                    Assert.That( iWithTag.ValidReleaseTag, Is.Null );
                    Assert.That( iWithTag.Error, Is.Not.Null );
                }
            };

            // The version on the commit point.
            {
                var i = repoTest.GetRepositoryInfo( tagged.Sha, overrides );
                Assert.That( i.FinalVersion.ToString(), Is.EqualTo( "0.0.0-a" ) );
                CollectionAssert.AreEqual( CSVersion.FirstPossibleVersions, i.PossibleVersions );
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
        public void ignoring_legacy_versions_with_StartingVersionForCSemVer_option()
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( cOK.Sha, overrides );
                Assert.That( i.ReleaseTagError, Is.Not.Null );
            }
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cOK.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersionForCSemVer = "4.0.3-beta"
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.ValidReleaseTag.ToString(), Is.EqualTo( "4.0.3-b" ) );
                //Assert.That( i.CommitVersionInfo.PreviousCommit, Is.Null );
                CollectionAssert.AreEqual( i.PossibleVersions.Select( t => t.ToString() ), new[] { "4.0.3-b" } );
            }
            {
                var cAbove = repoTest.Commits.First( sc => sc.Message.StartsWith( "Second b/b2" ) );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cAbove.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersionForCSemVer = "4.0.3-beta"
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.CommitInfo.BasicInfo.BestCommitBelow.ThisTag.ToString(), Is.EqualTo( "4.0.3-b" ) );
                Assert.That( i.ValidReleaseTag, Is.Null );
                CollectionAssert.Contains( i.PossibleVersions.Select( t => t.ToString() ), "4.0.3-b00-01", "4.0.3-b01", "4.0.3-d", "4.0.3", "4.1.0-r", "4.1.0", "5.0.0" );
            }

            // Commit before the StartingVersionForCSemVer has no PossibleVersions.
            {
                var cBelow = repoTest.Commits.First( sc => sc.Message.StartsWith( "On master again" ) );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cBelow.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersionForCSemVer = "4.0.3-beta"
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.CommitInfo.BasicInfo, Is.Null );
                Assert.That( i.ValidReleaseTag, Is.Null );
                CollectionAssert.IsEmpty( i.PossibleVersions );
            }
            {
                var cBelow = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'a' into b" ) );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cBelow.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersionForCSemVer = "4.0.3-beta"
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.CommitInfo.BasicInfo, Is.Null );
                Assert.That( i.ValidReleaseTag, Is.Null );
                CollectionAssert.IsEmpty( i.PossibleVersions );
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.CommitInfo.BasicInfo.BestCommitBelow.ThisTag, Is.EqualTo( v1beta ) );
                Assert.That( i.ValidReleaseTag, Is.Null );
                CollectionAssert.AreEqual( v1beta.GetDirectSuccessors(), i.PossibleVersions );
            }

            var cAlphaContinue = repoTest.Commits.First( sc => sc.Message.StartsWith( "Dev again in Alpha." ) );
            // We set 2.0.0 on cReleased. Its content is the same as cAlpha (mege commits with no changes).
            // To be able to do this we NEED to use the StartingVersionForCSemVer
            //
            // cAlphaContinue
            //   |
            //   |    cReleased - v2.0.0
            //   |  /
            //   |/
            // cAlpha - v1.0.0-beta

            overrides.MutableAdd( cReleased.Sha, "2.0.0" );
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                i.ReleaseTagError.Should().NotBeNull();
                i.ValidReleaseTag.Should().BeNull();
            }
            // Use the StartingVersionForCSemVer:
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingVersionForCSemVer = "2.0.0",
                    StartingCommitSha = cReleased.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.ValidReleaseTag.ToString(), Is.EqualTo( "2.0.0" ) );
            }
            // Subsequent developments of alpha branch now starts after 2.0.0, for instance 2.1.0-beta.
            overrides.MutableAdd( cAlphaContinue.Sha, "2.1.0-beta" );
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cAlphaContinue.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                var tagged = CSVersion.TryParse( "2.1.0-beta" );
                Assert.That( i.ReleaseTagError, Is.Null );
                Assert.That( i.ValidReleaseTag, Is.EqualTo( tagged ) );
                // alpha branch can continue with any successors v2.0.0.
                CollectionAssert.AreEqual( CSVersion.TryParse( "2.0.0" ).GetDirectSuccessors(), i.PossibleVersions );
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                Assert.That( i.ValidReleaseTag, Is.EqualTo( CSVersion.TryParse( "v2.0.0" ) ) );
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cDevInBeta.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                Assert.That( i.ValidReleaseTag, Is.EqualTo( CSVersion.TryParse( "v1.0.1-b" ) ) );
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cDevInGamma.Sha,
                    OverriddenTags = overrides.Overrides
                } );
                Assert.That( i.ValidReleaseTag, Is.EqualTo( CSVersion.TryParse( "v1.0.1-a" ) ) );
            }
            // On "gamma" branch, the head is 7 commits ahead of the v2.0.0 tag: this is the longest path. 
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "gamma",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "gamma", CIBranchVersionMode.LastReleaseBased )
                    }
                } );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "2.0.1--0007-gamma" ) );
            }
            // Testing "gamma" branch in ZeroTimed mode. 
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "gamma",
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "gamma", CIBranchVersionMode.ZeroTimed )
                    }
                } );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "0.0.0--009y09h-gamma+v2.0.0" ) );
            }
            // On "alpha" branch, the head is 6 commits ahead of the v2.0.0 tag (always the take the longest path). 
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "alpha",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.LastReleaseBased, "ALPHAAAA" )
                    }
                } );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "2.0.1--0006-ALPHAAAA" ) );
            }
            // Testing "alpha" branch in ZeroTimed mode.  
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "alpha",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.ZeroTimed, "ALPH" )
                    }
                } );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "0.0.0--009y6hm-ALPH+v2.0.0" ) );
            }
            // On "beta" branch, the head is 6 commits ahead of the v2.0.0 tag. 
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "beta",
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( "beta", CIBranchVersionMode.LastReleaseBased, "BBBBBB" )
                    }
                } );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "2.0.1--0006-BBBBBB" ) );
            }
            // Testing ZeroTimed mode on "beta" branch. 
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = "beta",
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "beta", CIBranchVersionMode.ZeroTimed, "beta" )
                    }
                } );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( "0.0.0--009y087-beta+v2.0.0" ) );
            }

        }

        [TestCase( "v1.0.0", "alpha", null, "1.0.1--0001-alpha" )]
        [TestCase( "v1.0.0", "beta", null, "1.0.1--0001-beta" )]
        [TestCase( "v1.0.0", "parallel-world", "parallel", "1.0.1--0003-parallel" )]
        [TestCase( "v0.1.0-beta", "alpha", null, "0.1.0-b00-00-0001-alpha" )]
        [TestCase( "v0.0.0-rc", "beta", null, "0.0.0-r00-00-0001-beta" )]
        public void CIBuildVersion_from_RealDevInAlpha_commits_ahead_tests( string vRealDevInAlpha, string branchName, string branchVersionName, string ciBuildVersion )
        {
            var repoTest = TestHelper.TestGitRepository;
            var cRealDevInAlpha = repoTest.Commits.Single( sc => sc.Message.StartsWith( "Real Dev in Alpha." ) );
            var overrides = new TagsOverride().MutableAdd( cRealDevInAlpha.Sha, vRealDevInAlpha );
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = branchName,
                    OverriddenTags = overrides.Overrides,
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( branchName, CIBranchVersionMode.LastReleaseBased, branchVersionName )
                    }
                } );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( ciBuildVersion ) );
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
        public void CIBuildVersion_from_DevInAlpha_commits_ahead_tests( string vDevInAlpha, string branchName, string branchNameVersion, string ciBuildVersion )
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingBranchName = branchName,
                    OverriddenTags = overrides.Overrides,
                    Branches = 
                    {
                        new RepositoryInfoOptionsBranch( branchName, CIBranchVersionMode.LastReleaseBased, branchNameVersion )
                    }
                } );
                Assert.That( i.ValidReleaseTag, Is.Null );
                Assert.That( i.CIRelease.BuildVersion.NormalizedText, Is.EqualTo( ciBuildVersion ) );
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

                RepositoryInfo info;

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
                RepositoryInfo info;
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
            var cD = repoTest.Commits.First( sc => sc.Message.StartsWith( "D-Commit." ) );
            var cC = repoTest.Commits.First( sc => sc.Message.StartsWith( "C-Commit." ) );
            var cF = repoTest.Commits.First( sc => sc.Sha == "27a629754c6b9034f7ca580442b589a0241773c5" );
            var cB = repoTest.Commits.First( sc => sc.Message.StartsWith( "B-Commit." ) );
            var cA = repoTest.Commits.First( sc => sc.Message.StartsWith( "Merge branch 'fumble-develop' into fumble-master" ) );
            var cFix = repoTest.Commits.First( sc => sc.Sha == "e6766d127f9a2df42567151222c6569601614626" );
            var cX = repoTest.Commits.First( sc => sc.Message.StartsWith( "X-Commit." ) );
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
            //        |         => Publishing any 5.X.X versions from this commit would be silly!
            //        |            BetterExistingVersion is here for that!
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cFix.Sha
                } );
                // Above OR on the fix of the fumble commit, any successor of the 5.0.0 is possible
                // This iw where BetterExistingVersion must be checked!
                Assert.That( i.BetterExistingVersion.ThisTag, Is.EqualTo( v5 ) );
                CollectionAssert.AreEqual( v5.GetDirectSuccessors(), i.PossibleVersions );
                CollectionAssert.AreEqual( v5.GetDirectSuccessors(), i.NextPossibleVersions );
            }
            {
                // Above the fix of the fumble commit, any successor of the 5.0.0 is possible.
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cX.Sha
                } );
                CollectionAssert.AreEqual( v5.GetDirectSuccessors(), i.PossibleVersions );
                CollectionAssert.AreEqual( v5.GetDirectSuccessors(), i.NextPossibleVersions );
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
            // cFix    |   +         This commit point has the same content as cM (the "master").
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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cFix.Sha
                } );
                CollectionAssert.AreEqual( v10.GetDirectSuccessors(), i.PossibleVersions );

                RepositoryInfo iLTS = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cFix.Sha,
                    SingleMajor = 5
                } );
                var allowed = v5.GetDirectSuccessors().Where( v => v.Major == 5 );
                CollectionAssert.AreEqual( allowed, iLTS.PossibleVersions );

            }
            {
                // Without SingleMajor = 5, On B-Commit (just above v4.4.0-alpha) v10 wins.
                var v44a = CSVersion.TryParse( "v4.4.0-alpha" );
                var v44a01 = CSVersion.TryParse( "v4.4.0-alpha.0.1" );
                var v44a1 = CSVersion.TryParse( "v4.4.0-alpha.1" );
                var v500 = CSVersion.TryParse( "v5.0.0" );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cB.Sha
                } );
                CollectionAssert.AreEqual( v10.GetDirectSuccessors(), i.PossibleVersions );

                RepositoryInfo iLTS = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cB.Sha,
                    SingleMajor = 4
                } );
                var allowed = v44a.GetDirectSuccessors().Where( v => v.Major == 4 );
                CollectionAssert.AreEqual( allowed, iLTS.PossibleVersions );
            }
            {
                // On C-Extra: the v10.0.0 is actually not allowed since it is not a
                // successor of v4.3.2.
                // The possible versions here are the successors of v4.4.0-alpha (up to the
                // existing v5.0.0-rc) because its content is tagged with v4.4.0-alpha.
                var v432 = CSVersion.TryParse( "v4.3.2" );
                var v44a = CSVersion.TryParse( "v4.4.0-alpha" );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    OverriddenTags = overrides.Overrides,
                    StartingCommitSha = cExtra.Sha
                } );
                Assert.That( i.ReleaseTagIsNotPossibleError );
                CollectionAssert.AreEqual( v44a.GetDirectSuccessors().Where( v => v < v5rc ), i.PossibleVersions );
                // But the v10.0.0 tag exits, the versions above cExtra are
                CollectionAssert.AreEqual( v10.GetDirectSuccessors(), i.NextPossibleVersions );

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
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cRealDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides,
                } );
                i.ValidReleaseTag.Should().BeNull();
                i.Error.Trim().Should().Be( $"Commit '{cRealDevInAlpha.Sha}' has 2 different released version tags. Delete some of them or create +invalid tag(s) if they are already pushed to a remote repository." );
            }
            {
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions
                {
                    StartingCommitSha = cRealDevInAlpha.Sha,
                    OverriddenTags = overrides.Overrides,
                    StartingVersionForCSemVer = "2.0.0"
                } );
                i.ValidReleaseTag.Should().Be( CSVersion.Parse( "2.0.0" ) );
            }
        }

        [Test]
        public void when_there_is_no_versions_below()
        {
            var repoTest = TestHelper.TestGitRepository;
            {
                var cOrphan = repoTest.Commits.Single( sc => sc.Message.StartsWith( "First in parallel world." ) );
                RepositoryInfo i = repoTest.GetRepositoryInfo( new RepositoryInfoOptions { StartingCommitSha = cOrphan.Sha } );
                i.CommitInfo.BasicInfo.Should().BeNull();
                i.CommitInfo.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.CIRelease.Should().BeNull();
            }
            {
                var options = new RepositoryInfoOptions
                {
                    StartingBranchName = "alpha",
                    Branches =
                    {
                        new RepositoryInfoOptionsBranch( "alpha", CIBranchVersionMode.ZeroTimed )
                    }
                };

                RepositoryInfo i = repoTest.GetRepositoryInfo( options );
                i.CommitInfo.BasicInfo.Should().BeNull();
                i.CommitInfo.PossibleVersions.Should().BeEquivalentTo( CSVersion.FirstPossibleVersions );
                i.CIRelease.Should().NotBeNull();
            }
        }
    }
}
