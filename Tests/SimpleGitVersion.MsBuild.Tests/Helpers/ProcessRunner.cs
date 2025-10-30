using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SimpleGitVersion.MSBuild.Tests;

/// <summary>
/// Basic external process runner encapsulation.
/// </summary>
public static class ProcessRunner
{
    static readonly CKTrait _stdErrTag = ActivityMonitor.Tags.Register( "StdErr" );
    static readonly CKTrait _stdOutTag = ActivityMonitor.Tags.Register( "StdOut" );

    /// <summary>
    /// Starts and wait for the end of an external process.
    /// </summary>
    /// <param name="logger">The logger to use. Will receive standard errors and outputs.</param>
    /// <param name="fileName">The file name to run.</param>
    /// <param name="arguments">Command line arguments.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="environmentVariables">Optional environment variables to configure.</param>
    /// <returns>The exit status code.</returns>
    public static int RunProcess( IActivityLineEmitter logger,
                                  string fileName,
                                  string arguments,
                                  string workingDirectory,
                                  Dictionary<string, string>? environmentVariables )
    {
        using Process process = DoRun( logger, fileName, arguments, workingDirectory, environmentVariables, Encoding.UTF8 );
        process.WaitForExit();
        return process.ExitCode;
    }


    /// <summary>
    /// Starts and wait for the end of an external process until a <paramref name="timemout"/> occurs.
    /// </summary>
    /// <param name="logger">The logger to use. Will receive standard errors and outputs.</param>
    /// <param name="fileName">The file name to run.</param>
    /// <param name="arguments">Command line arguments.</param>
    /// <param name="workingDirectory">Working directory.</param>
    /// <param name="environmentVariables">Optional environment variables to configure.</param>
    /// <param name="timemout">Timeout in milliseconds.</param>
    /// <returns>The exit status code.</returns>
    public static int? RunProcess( IActivityLineEmitter logger,
                                   string fileName,
                                   string arguments,
                                   string workingDirectory,
                                   Dictionary<string, string>? environmentVariables,
                                   int timemout )
    {
        using Process process = DoRun( logger, fileName, arguments, workingDirectory, environmentVariables, Encoding.UTF8 );
        bool exited = process.WaitForExit( timemout );
        if( exited )
        {
            // Ensure completed asynchronous event handling.
            // See https://github.com/NuGet/Home/issues/10189
            process.WaitForExit();
        }
        return exited ? process.ExitCode : null;
    }

    static Process DoRun( IActivityLineEmitter logger,
                          string fileName,
                          string arguments,
                          string workingDirectory,
                          Dictionary<string, string>? environmentVariables,
                          Encoding outputEncoding )
    {
        var info = new ProcessStartInfo( fileName, arguments )
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = outputEncoding,
            StandardErrorEncoding = outputEncoding
        };
        if( environmentVariables != null && environmentVariables.Count > 0 )
        {
            foreach( var kv in environmentVariables ) info.EnvironmentVariables.Add( kv.Key, kv.Value );
        }
        var process = new Process { StartInfo = info };
        process.OutputDataReceived += ( sender, data ) =>
        {
            if( data.Data != null ) logger.Trace( _stdOutTag, data.Data );
        };
        process.ErrorDataReceived += ( sender, data ) =>
        {
            if( data.Data != null ) logger.Trace( _stdErrTag, data.Data );
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }
}
