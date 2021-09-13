using Cake.Arguments;
using CK.Core;
using CK.Monitoring;
using CK.Monitoring.Handlers;
using CodeCakeBuilder.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeCake
{
    class Program
    {
        /// <summary>
        /// Basic parameter that sets the solution directory as being the current directory
        /// instead of using the default lookup to "Solution/Builder/bin/[Configuration]/[targetFramework]" folder.
        /// Check of this argument uses <see cref="StringComparer.OrdinalIgnoreCase"/>.
        /// </summary>
        const string SolutionDirectoryIsCurrentDirectoryParameter = "SolutionDirectoryIsCurrentDirectory";

        /// <summary>
        /// CodeCakeBuilder entry point. This is a default, simple, implementation that can 
        /// be extended as needed.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>An error code (typically negative), 0 on success.</returns>
        static async Task<int> Main( string[] args )
        {
            GrandOutputConfiguration cfg = new();
            cfg.AddHandler( new ConsoleConfiguration() );
            GrandOutput.EnsureActiveDefault( cfg );
            ArgumentParser parser = new();
            var m = new ActivityMonitor();
            try
            {

                CCBOptions options = parser.Parse( m, args ) ?? new CCBOptions();
                string? solutionDirectory = args.Contains( SolutionDirectoryIsCurrentDirectoryParameter, StringComparer.OrdinalIgnoreCase )
                                            ? Environment.CurrentDirectory
                                            : null;
                Build app = new();
                bool result = await app.RunAsync( m, options, solutionDirectory );
                if( app.GlobalInfo.InteractiveMode == InteractiveMode.Interactive )
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine( $"Hit any key to exit." );
                    System.Console.WriteLine( $"Use -{Interactive.NoInteractionArgument} or -{Interactive.AutoInteractionArgument} parameter to exit immediately." );
                    System.Console.ReadKey();
                }
                return result ? 0 : 1;
            }
            catch( Exception e )
            {
                m.Error( e );
                return 1;
            }
        }
    }
}
