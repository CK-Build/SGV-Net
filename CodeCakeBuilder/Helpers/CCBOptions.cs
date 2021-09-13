using CK.Core;
using CodeCake;
using System;
using System.Collections.Generic;
using System.Text;

namespace CodeCakeBuilder.Helpers
{
    public class CCBOptions
    {
        public InteractiveMode InteractiveMode { get; set; } = InteractiveMode.Interactive;

        /// <summary>
        /// Gets or sets the output verbosity.
        /// </summary>
        /// <value>The output verbosity.</value>
        public LogFilter Verbosity { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to show task descriptions.
        /// </summary>
        /// <value>
        ///   <c>true</c> to show task description; otherwise, <c>false</c>.
        /// </value>
        public bool ShowDescription { get; internal set; }
        /// <summary>
        /// Gets or sets a value indicating whether to show help.
        /// </summary>
        /// <value>
        ///   <c>true</c> to show help; otherwise, <c>false</c>.
        /// </value>
        public bool ShowHelp { get; internal set; }
        /// <summary>
        /// Gets or sets a value indicating whether to show version information.
        /// </summary>
        /// <value>
        ///   <c>true</c> to show version information; otherwise, <c>false</c>.
        /// </value>
        public bool ShowVersion { get; internal set; }
        /// <summary>
        /// Gets the script arguments.
        /// </summary>
        /// <value>The script arguments.</value>
        public IDictionary<string, string> Arguments { get; internal set; }
        /// <summary>
        /// Gets or sets the build script.
        /// </summary>
        /// <value>The build script.</value>
        public string Script { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="CakeOptions"/> class.
        /// </summary>
        public CCBOptions()
        {
            Arguments = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
            Verbosity = LogFilter.Terse;
        }
    }
}
