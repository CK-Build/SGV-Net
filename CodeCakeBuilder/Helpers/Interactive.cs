using CK.Core;
using System;
using System.Linq;

namespace CodeCake
{
    public static class Interactive
    {
        /// <summary>
        /// The "nointeraction" string with no dash before.
        /// </summary>
        public static readonly string NoInteractionArgument = "nointeraction";

        /// <summary>
        /// The "autointeraction" string with no dash before.
        /// </summary>
        public static readonly string AutoInteractionArgument = "autointeraction";

        /// <summary>
        /// Retrieves the value of the environment variable or null if the environment variable do not exist
        /// and can not be given by the user.
        /// In -autointeraction mode, the value can be provided on the commannd line using -ENV:<paramref name="variable"/>=... parameter.
        /// </summary>
        /// <param name="variable">The environment variable.</param>
        /// <param name="setCache">By default, if the value is interactively read, it is stored in the process environment variables.</param>
        /// <returns>Retrieves the value of the environment variable or null if the environment variable do not exist.</returns>
        public static string? InteractiveEnvironmentVariable( this StandardGlobalInfo @this, IActivityMonitor m, string variable, bool setCache = true )
        {
            string? v = Environment.GetEnvironmentVariable( variable );
            var mode = @this.InteractiveMode;
            if( v == null && mode != InteractiveMode.NoInteraction )
            {
                Console.Write( $"Environment Variable '{variable}' not found. Enter its value: " );
                if( mode == InteractiveMode.AutoInteraction )
                {
                    string fromArgName = "ENV:" + variable;
                    string? fromArg = @this.Arguments.HasArgument( fromArgName ) ? @this.Arguments.GetArgument( fromArgName ) : null;
                    if( fromArg != null )
                    {
                        Console.WriteLine( v = fromArg );
                        m.Info( $"Mode -autointeraction: automatically answer with command line -{fromArgName}={fromArg} argument." );
                    }
                    else
                    {
                        Console.WriteLine( v = String.Empty );
                        m.Info( $"Mode -autointeraction (and no command line -{fromArgName}=XXX argument): automatically answer with an empty string." );
                    }
                }
                else v = Console.ReadLine();
                if( setCache ) Environment.SetEnvironmentVariable( variable, v );
            }
            return v;
        }


        /// <summary>
        /// Prompts the user for one of the <paramref name="options"/> characters that MUST be uppercase after
        /// having looked for a program argument that answers the prompt (ex: -RunUnitTests=N).
        /// <see cref="InteractiveMode()"/> must be <see cref="CodeCake.InteractiveMode.AutoInteraction"/>
        /// or <see cref="CodeCake.InteractiveMode.Interactive"/> otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="globalInfo">The context.</param>
        /// <param name="argumentName">Name of the command line argument.</param>
        /// <param name="message">
        /// Message that will be displayed in front of the input. 
        /// When null, no message is displayed, when not null, the options are automatically displayed: (Y/N/C).
        /// </param>
        /// <param name="options">Allowed characters that must be uppercase.</param>
        /// <returns>The entered char (always in uppercase). Necessarily one of the <paramref name="options"/>.</returns>
        public static char ReadInteractiveOption( this StandardGlobalInfo globalInfo, IActivityMonitor m, string argumentName, string message, params char[] options )
        {
            if( String.IsNullOrWhiteSpace( argumentName ) ) throw new ArgumentException( "Must be a non empty string.", nameof( argumentName ) );
            return DoReadInteractiveOption( m, globalInfo, argumentName, message, options );
        }

        static char DoReadInteractiveOption( IActivityMonitor m, StandardGlobalInfo globalInfo, string argumentName, string message, char[] options )
        {
            if( options == null || options.Length == 0 ) throw new ArgumentException( "At least one (uppercase) character for options must be provided." );
            var mode = globalInfo.InteractiveMode;
            if( mode == CodeCake.InteractiveMode.NoInteraction ) throw new InvalidOperationException( "Interactions are not allowed." );
            if( options.Any( c => char.IsLower( c ) ) ) throw new ArgumentException( "Options must be uppercase letter." );

            string choices = String.Join( "/", options );
            if( string.IsNullOrWhiteSpace( message ) )
                Console.Write( "{0}: ", choices );
            else Console.Write( "{0} ({1}): ", message, choices );

            if( argumentName != null && globalInfo.Arguments.HasArgument( argumentName ) )
            {
                string? arg = globalInfo.Arguments.GetArgument( argumentName );
                if( arg.Length != 1
                    || !options.Contains( char.ToUpperInvariant( arg[0] ) ) )
                {
                    Console.WriteLine();
                    m.Error( $"Provided command line argument -{argumentName}={arg} is invalid. It must be a unique character in: {choices}" );
                    // Fallback to interactive mode below.
                }
                else
                {
                    var c = char.ToUpperInvariant( arg[0] );
                    Console.WriteLine( c );
                    m.Info( $"Answered by command line argument -{argumentName}={arg}." );
                    return c;
                }
            }
            if( mode == CodeCake.InteractiveMode.AutoInteraction )
            {
                char c = options[0];
                Console.WriteLine( c );
                if( argumentName != null )
                {
                    m.Info( $"Mode -autointeraction (and no command line -{argumentName}=\"value\" argument found): automatically answer with the first choice: {c}." );
                }
                else
                {
                    m.Info( $"Mode -autointeraction: automatically answer with the first choice: {c}." );
                }
                return c;
            }
            for(; ; )
            {
                char c = char.ToUpperInvariant( Console.ReadKey().KeyChar );
                Console.WriteLine();
                if( options.Contains( c ) ) return c;
                Console.Write( $"Invalid choice '{c}'. Must be one of {choices}: " );
            }
        }
    }
}
