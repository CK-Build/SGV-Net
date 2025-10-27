using SimpleGitVersion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

var logger = new Logger();

var info = CommitInfo.LoadFromPath( logger, Environment.CurrentDirectory );
if( info.Error != null )
{
    info.Explain( logger );
    return -1;
}
return 0;

sealed class Logger : ILogger
{
    public void Error( string msg ) => Console.Error.WriteLine( msg );

    public void Info( string msg ) => Console.Out.WriteLine( msg );

    public void Warn( string msg ) => Console.Out.WriteLine( msg );
}

