using System;

namespace SimpleGitVersion.Core.Tests;

public class ConsoleLogger : ILogger
{
    public void Error( string msg )
    {
        Console.WriteLine( "Error: " + msg );
    }
    public void Warn( string msg )
    {
        Console.WriteLine( "Warn: " + msg );
    }

    public void Info( string msg )
    {
        Console.WriteLine( "Info: " + msg );
    }

}
