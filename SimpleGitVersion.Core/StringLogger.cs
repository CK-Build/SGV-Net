using System;
using System.Text;

namespace SimpleGitVersion;

/// <summary>
/// Logger implementation in a string.
/// </summary>
public sealed class StringLogger : ILogger
{
    readonly StringBuilder _b;

    public StringLogger()
    {
        _b = new StringBuilder( 4096 );
        _b.Append( "SimpleGitVersion:" ).AppendLine();
    }

    /// <summary>
    /// Gets the inner builder.
    /// </summary>
    public StringBuilder Builder => _b;

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

    /// <summary>
    /// Appends the lines in a "| [Error] " block.
    /// </summary>
    /// <param name="msg">The error.</param>
    public void Error( string msg ) => Append( "| [Error] ", "|         ", msg );

    /// <summary>
    /// Appends the lines with "| " prefix.
    /// </summary>
    /// <param name="msg">The error.</param>
    public void Info( string msg ) => Append( "| ", "| ", msg );

    /// <summary>
    /// Appends the lines in a "| [Warn] " block.
    /// </summary>
    /// <param name="msg">The error.</param>
    public void Warn( string msg ) => Append( "| [Warn] ", "|        ", msg );

    /// <summary>
    /// Should be called at the end of the operation. Adds the final version and build configuration.
    /// </summary>
    /// <param name="final"></param>
    /// <returns></returns>
    public string Conclude( ICommitBuildInfo final )
    {
        return _b.Append( "|=> " )
                 .Append( final.Version )
                 .Append( " (Configuration: " ).Append( final.BuildConfiguration )
                 .Append( ')' ).AppendLine().ToString();
    }
}
