using CK.Core;
using CodeCake;
using CodeCakeBuilder.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Cake.Arguments
{
    internal sealed class ArgumentParser
    {
        private static bool IsQuoted( string value )
        {
            return value.StartsWith( "\"", StringComparison.OrdinalIgnoreCase )
                   && value.EndsWith( "\"", StringComparison.OrdinalIgnoreCase );
        }

        /// <summary>
        /// Unquote the specified <see cref="System.String"/>.
        /// </summary>
        /// <param name="value">The string to unquote.</param>
        /// <returns>An unquoted string.</returns>
        static string UnQuote( string value )
        {
            if( IsQuoted( value ) )
            {
                value = value.Trim( '"' );
            }
            return value;
        }
        public CCBOptions? Parse( IActivityMonitor m, IEnumerable<string> args )
        {
            if( args == null )
            {
                throw new ArgumentNullException( "args" );
            }

            var options = new CCBOptions();
            var isParsingOptions = false;

            var arguments = args.ToList();

            // If we don't have any arguments, search for a default script.
            if( arguments.Count == 0 )
            {
                options.Script = GetDefaultScript( m );
            }

            foreach( var arg in arguments )
            {
                var value = UnQuote( arg );

                if( isParsingOptions )
                {
                    if( IsOption( value ) )
                    {
                        if( !ParseOption( m, value, options ) ) return null;
                    }
                    else
                    {
                        m.Error( "More than one build script specified." );
                        return null;
                    }
                }
                else
                {
                    try
                    {
                        // If they didn't provide a specific build script, search for a defualt.
                        if( IsOption( arg ) )
                        {
                            // Make sure we parse the option
                            if( !ParseOption( m, value, options ) )
                            {
                                return null;
                            }

                            options.Script = GetDefaultScript( m );
                            continue;
                        }

                        // Quoted?
                        options.Script = value;
                    }
                    finally
                    {
                        // Start parsing options.
                        isParsingOptions = true;
                    }
                }
            }

            return options;
        }

        private static bool IsOption( string arg )
        {
            if( string.IsNullOrWhiteSpace( arg ) )
            {
                return false;
            }
            return arg[0] == '-';
        }

        private bool ParseOption( IActivityMonitor m, string arg, CCBOptions options )
        {
            string name, value;

            var separatorIndex = arg.IndexOfAny( new[] { '=' } );
            if( separatorIndex < 0 )
            {
                name = arg.Substring( 1 );
                value = string.Empty;
            }
            else
            {
                name = arg.Substring( 1, separatorIndex - 1 );
                value = arg.Substring( separatorIndex + 1 );
            }

            if( value.Length > 2 )
            {
                if( value[0] == '\"' && value[value.Length - 1] == '\"' )
                {
                    value = value.Substring( 1, value.Length - 2 );
                }
            }

            return ParseOption( m, name, value, options );
        }

        private bool ParseOption( IActivityMonitor m, string name, string value, CCBOptions options )
        {
            if( name.Equals( "verbosity", StringComparison.OrdinalIgnoreCase )
                || name.Equals( "v", StringComparison.OrdinalIgnoreCase ) )
            {
                // Parse verbosity.
                if( !LogFilter.TryParse( value, out LogFilter f ) ) return false;
                options.Verbosity = f;

            }

            if( name.Equals( "showdescription", StringComparison.OrdinalIgnoreCase ) ||
                name.Equals( "s", StringComparison.OrdinalIgnoreCase ) )
            {
                options.ShowDescription = true;
            }

            //if( name.Equals( "dryrun", StringComparison.OrdinalIgnoreCase ) || //We can do it, but it was not implemented, but we dont use it.
            //    name.Equals( "noop", StringComparison.OrdinalIgnoreCase ) ||
            //    name.Equals( "whatif", StringComparison.OrdinalIgnoreCase ) )
            //{
            //    options.PerformDryRun = true;
            //}

            if( name.Equals( "help", StringComparison.OrdinalIgnoreCase ) ||
                name.Equals( "?", StringComparison.OrdinalIgnoreCase ) )
            {
                options.ShowHelp = true;
            }

            if( name.Equals( "version", StringComparison.OrdinalIgnoreCase ) ||
                name.Equals( "ver", StringComparison.OrdinalIgnoreCase ) )
            {
                options.ShowVersion = true;
            }

            if( options.Arguments.ContainsKey( name ) )
            {
                m.Error( $"Multiple arguments with the same name ({name})." );
                return false;
            }

            if( name.Equals( Interactive.NoInteractionArgument ) ) options.InteractiveMode = InteractiveMode.NoInteraction;
            if( name.Equals( Interactive.AutoInteractionArgument ) ) options.InteractiveMode = InteractiveMode.AutoInteraction;

            options.Arguments.Add( name, value );
            return true;
        }

        private string GetDefaultScript( IActivityMonitor m )
        {
            m.Trace( "Using default 'Build' script..." );
            return "Build";
        }
    }
}
