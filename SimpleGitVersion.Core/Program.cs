using CSemVer;
using SimpleGitVersion;
using System;
using System.IO;
using System.Text;

var logger = new Logger();

string? outPath = null;
int idxOutDir = Array.IndexOf( args, "--out-file" );
if( idxOutDir >= 0 )
{
    if( args.Length == idxOutDir )
    {
        logger.Error( "Missing output file path argument." );
    }
    else
    {
        outPath = args[idxOutDir + 1];
        if( string.IsNullOrWhiteSpace( outPath ) || outPath.AsSpan().ContainsAny( Path.GetInvalidPathChars() ) )
        {
            logger.Error( "Invalid output file path." );
            outPath = null;
        }
        else
        {
            try
            {
                outPath = Path.GetFullPath( Path.Combine( Environment.CurrentDirectory, outPath ) );
                var directory = Path.GetDirectoryName( outPath );
                if( directory == null )
                {
                    logger.Error( "Invalid output file path." );
                    outPath = null;
                }
                else
                {
                    if( !Directory.Exists( directory ) )
                    {
                        logger.Info( $"Creating output folder '{directory}'." );
                        Directory.CreateDirectory( directory );
                    }
                }
            }
            catch( Exception ex )
            {
                logger.Error( $"Error while computing output directory '{outPath}' ({ex.Message})." );
                outPath = null;
            }
        }
    }
}

var info = CommitInfo.LoadFromPath( logger, Environment.CurrentDirectory );
info.Explain( logger );
if( outPath != null )
{
    var finalBuildInfo = info.FinalBuildInfo;
    File.WriteAllText( outPath, $"""
    {finalBuildInfo.Version}
    {finalBuildInfo.AssemblyVersion}
    {finalBuildInfo.FileVersion}
    {finalBuildInfo.InformationalVersion}
    {info.RepositoryInfo.RemoteUrl}
    
    """ );
}
Console.Write( logger.Conclude( info.FinalVersion ) );
return info.Error != null ? -1 : 0;

sealed class Logger : ILogger
{
    readonly StringBuilder _b;

    public Logger()
    {
        _b = new StringBuilder( 4096 );
        _b.Append( "SimpleGitVersion:" ).AppendLine();
    }

    void Append( string header, string prefix, string msg )
    {
        _b.Append( header );
        var lines = msg.AsSpan().EnumerateLines();
        if( lines.MoveNext() )
        {
            _b.Append( lines.Current ).AppendLine();
            while( lines.MoveNext() )
            {
                _b.Append( prefix ).Append( lines.Current ).AppendLine();
            }
        }
        else
        {
            _b.AppendLine();
        }
    }

    public void Error( string msg ) => Append( "| [Error] ", "|         ", msg );

    public void Info( string msg ) => Append( "| ", "| ", msg );

    public void Warn( string msg ) => Append( "| [Warn] ", "|        ", msg );

    public string Conclude( SVersion v ) => _b.Append( "|=> " ).Append( v ).AppendLine().ToString();
}

