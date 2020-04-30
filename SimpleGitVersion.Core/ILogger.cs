using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleGitVersion
{
    /// <summary>
    /// Simple logger abstraction.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs an error.
        /// </summary>
        /// <param name="msg">Error message.</param>
        void Error( string msg );
        
        /// <summary>
        /// logs a warning.
        /// </summary>
        /// <param name="msg">Warning message.</param>
        void Warn( string msg );

        /// <summary>
        /// Logs a trace or informational message.
        /// </summary>
        /// <param name="msg">Message.</param>
        void Info( string msg );
    }

}
