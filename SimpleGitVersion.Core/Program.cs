using SimpleGitVersion;
using System;
using System.Diagnostics;
using System.IO;

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
                    logger.Info( $"Creating output folder '{directory}'." );
                    if( !Directory.Exists( directory ) ) Directory.CreateDirectory( directory );
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
return info.Error != null ? -1 : 0;

sealed class Logger : ILogger
{
    public void Error( string msg ) => Console.Error.WriteLine( msg );

    public void Info( string msg ) => Console.Out.WriteLine( msg );

    public void Warn( string msg ) => Console.Out.WriteLine( msg );
}

