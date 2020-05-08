using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using NUnit.Framework;

namespace SimpleGitVersion
{
    static class TestHelper
    {
        static string _solutionFolder;
        static string _testGitRepositoryFolder;
        static RepositoryTester _testGitRepository;
        static object _lock = new object();

        public static string SolutionFolder
        {
            get
            {
                if( _solutionFolder == null ) InitalizePaths();
                return _solutionFolder;
            }
        }

        public static string TestGitRepositoryFolder
        {
            get
            {
                if( _testGitRepositoryFolder == null )
                {
                    lock ( _lock )
                    {
                        if( _testGitRepositoryFolder == null )
                        {
                            _testGitRepositoryFolder = Path.GetFullPath( Path.Combine( SolutionFolder, @"Tests\TestGitRepository" ) );
                            string gitPath = _testGitRepositoryFolder + @"\.git";
                            if( !Directory.Exists( gitPath ) )
                            {
                                // Let any exceptions be thrown here: if we can't have a copy of the test repository, it 
                                // is too risky to Assume(false).
                                Directory.CreateDirectory( _testGitRepositoryFolder );
                                gitPath = Repository.Clone( "https://github.com/CK-Build/TestGitRepository.git", _testGitRepositoryFolder );
                            }
                            try
                            {
                                using( var r = new Repository( gitPath ) )
                                {
                                    Commands.Fetch( r, "origin", Enumerable.Empty<string>(), new FetchOptions() { TagFetchMode = TagFetchMode.All }, "Testing." );
                                }
                            }
                            catch( LibGit2SharpException ex )
                            {
                                // Fetch fails. We don't care.
                                Console.WriteLine( "Warning: Fetching the TestGitRepository (https://github.com/CK-Build/TestGitRepository.git) failed. Check the internet connection. Error: {0}.", ex.Message );
                            }
                        }
                    }
                }
                return _testGitRepositoryFolder;
            }
        }

        static public RepositoryTester TestGitRepository
        {
            get { return _testGitRepository ?? (_testGitRepository = new RepositoryTester( TestGitRepositoryFolder ));}
        }

        public static string RepositoryXSDPath
        {
            get { return Path.Combine( TestHelper.SolutionFolder, "SimpleGitVersion.Core", "RepositoryInfo.xsd" ); }
        }


        static void InitalizePaths()
        {
            string p = new Uri( System.Reflection.Assembly.GetExecutingAssembly().CodeBase ).LocalPath;
            do
            {
                p = Path.GetDirectoryName( p );
            }
            while( !Directory.Exists( Path.Combine( p, ".git" ) ) );
            _solutionFolder = p;
        }

    }
}
