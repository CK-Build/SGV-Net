﻿using Cake.Common;
using Cake.Core;
using Cake.Core.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeCake
{
    /// <summary>
    /// Contains functionality related to the interactive mode.
    /// </summary>
    [CakeAliasCategory( "Environment" )]
    public static class InteractiveAliases
    {
        /// <summary>
        /// Gets whether the context supports interaction with the user.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>True if interactive mode is available, false otherwise.</returns>
        [CakeAliasCategory( "Interactive mode" )]
        [CakeMethodAlias]
        public static bool IsInteractiveMode( this ICakeContext context )
        {
            return !context.HasArgument( "-nointeractive" );
        }

        /// <summary>
        /// Retrieves the value of the environment variable or null if the environment variable do not exist
        /// and can not be given by the user.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="variable">The environment variable.</param>
        /// <returns>Retrieves the value of the environment variable or null if the environment variable do not exist.</returns>
        [CakeAliasCategory( "Environment Variables" )]
        [CakeMethodAlias]
        public static string InteractiveEnvironmentVariable( this ICakeContext context, string variable )
        {
            string v = context.EnvironmentVariable( variable );
            if( v == null && IsInteractiveMode( context ) )
            {
                Console.Write( "Environment Variable '{0}' not found. Enter its value: " );
                v = Console.ReadLine();
            }
            return v;
        }
    }
}