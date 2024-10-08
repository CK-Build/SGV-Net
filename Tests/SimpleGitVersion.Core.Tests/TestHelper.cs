using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;

namespace SimpleGitVersion;

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

    public static string TestsFolder => Path.Combine( SolutionFolder, "Tests" );

    public static string TestGitRepositoryFolder
    {
        get
        {
            if( _testGitRepositoryFolder == null )
            {
                lock( _lock )
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
        get { return _testGitRepository ?? (_testGitRepository = new RepositoryTester( TestGitRepositoryFolder )); }
    }


    static void InitalizePaths()
    {
        string p = System.Reflection.Assembly.GetExecutingAssembly().Location;
        do
        {
            p = Path.GetDirectoryName( p );
        }
        while( !Directory.Exists( Path.Combine( p, ".git" ) ) );
        _solutionFolder = p;
    }

}
