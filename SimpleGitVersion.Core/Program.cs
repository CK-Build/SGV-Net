using SimpleGitVersion;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

var logger = new StringLogger();

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
var finalBuildInfo = info.FinalBuildInfo;
if( outPath != null )
{
    File.WriteAllText( outPath, $"""
    {finalBuildInfo.Version}
    {finalBuildInfo.AssemblyVersion}
    {finalBuildInfo.FileVersion}
    {finalBuildInfo.InformationalVersion}
    {finalBuildInfo.BuildConfiguration}
    {info.RepositoryInfo.RemoteUrl}
    
    """ );
}
Console.Write( logger.Conclude( finalBuildInfo ) );
return info.Error != null ? -1 : 0;


