using FluentAssertions;
using LibGit2Sharp;
using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace SimpleGitVersion.Core.Tests
{
    [TestFixture]
    public class SimpleMiscTests
    {
        [Test]
        public void sha1_of_trees_rocks()
        {
            var first = TestHelper.TestGitRepository.Commits.First( sc => sc.Message.StartsWith( "First in parallel world." ) );
            var modified = TestHelper.TestGitRepository.Commits.First( sc => sc.Message.StartsWith( "Change in parallel-world.txt content (1)." ) );
            var reset = TestHelper.TestGitRepository.Commits.First( sc => sc.Message.StartsWith( "Reset change in parallel-world.txt content (2)." ) );
            using( var r = new Repository( TestHelper.TestGitRepositoryFolder ) )
            {
                var cFirst = r.Lookup<Commit>( first.Sha );
                var cModified = r.Lookup<Commit>( modified.Sha );
                var cReset = r.Lookup<Commit>( reset.Sha );
                Assert.That( cFirst.Tree.Sha, Is.Not.EqualTo( cModified.Tree.Sha ) );
                Assert.That( cReset.Tree.Sha, Is.Not.EqualTo( cModified.Tree.Sha ) );
                Assert.That( cFirst.Tree.Sha, Is.EqualTo( cReset.Tree.Sha ) );
            }
        }

        [Test]
        public void testing_connection_to_this_repository()
        {
            using( var thisRepo = new Repository( Repository.Discover( TestHelper.SolutionFolder ) ) )
            {
                Console.WriteLine( "This repo has {0} commits", thisRepo.Commits.Count() );
            }
        }

        [Test]
        public void testing_RepositoryInfo_on_this_repository()
        {
            var info = CommitInfo.LoadFromPath( new ConsoleLogger(), TestHelper.SolutionFolder, (logger, hasRepoXml,opt) =>
            {
                logger.Info( "Ignoring DirtyWorkingFolder check." );
                opt.IgnoreDirtyWorkingFolder = true;
            } );
            Console.WriteLine( $"This repo's SemVer: {info.FinalVersion}, InformationalVersion: '{info.FinalInformationalVersion}'." );
        }

        [Test]
        public void when_the_repo_is_shallow_cloned_there_is_no_exception_and_the_version_is_what_it_is()
        {
            // Here we don't have any verion tag in the parents: magically, we produce a "0.0.0--031t8rf-develop"
            // that as a Zero Version is safe to use.
            var targetDir = Path.Combine( TestHelper.TestsFolder, "ThisRepoDevDepth5" );
            if( !Directory.Exists( targetDir ) )
            {
                ZipFile.ExtractToDirectory( Path.Combine( TestHelper.TestsFolder, "ThisRepoDevDepth5.zip" ), targetDir );
            }
            var info = CommitInfo.LoadFromPath( new ConsoleLogger(), targetDir );
            info.FinalVersion.ToString().Should().Be( "0.0.0--031t8rf-develop" );
        }

        [Test]
        [Explicit]
        public void testing_SimpleGitRepositoryInfo_on_other_repository()
        {
            var info = CommitInfo.LoadFromPath( new ConsoleLogger(), @"C:\Dev\CK-Build\CK-Build\SGV-Net\Tests\ThisRepoDevDepth5" );
            Console.WriteLine( $"This repo's InformationalVersion: '{info.FinalInformationalVersion}'." );
        }
    }
}
