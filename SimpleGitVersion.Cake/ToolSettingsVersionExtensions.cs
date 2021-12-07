using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNetCore;
using Cake.Common.Tools.MSBuild;
using Cake.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleGitVersion
{
    /// <summary>
    /// Provides AddVersionArguments on <see cref="MSBuildSettings"/> and <see cref="DotNetCoreSettings"/>.
    /// </summary>
    public static class ToolSettingsSettingsVersionExtension
    {
        /// <summary>
        /// Adds standard version information on <see cref="DotNetSettings"/> objects.
        /// </summary>
        /// <typeparam name="T">Specialized DotNetCoreSettings type.</typeparam>
        /// <param name="this">This settings.</param>
        /// <param name="info">The commit build information.</param>
        /// <param name="conf">Optional configuration to apply after version arguments have been injected.</param>
        /// <returns>This settings.</returns>
        public static T AddVersionArguments<T>( this T @this, ICommitBuildInfo info, Action<T> conf = null ) where T : DotNetSettings
        {
            AddVersionToolArguments( @this, info );
            conf?.Invoke( @this );
            return @this;
        }

        /// <summary>
        /// Adds standard version information on <see cref="MSBuildSettings"/>.
        /// </summary>
        /// <param name="this">This settings.</param>
        /// <param name="info">The commit build information.</param>
        /// <param name="conf">Optional configuration to apply after version arguments have been injected.</param>
        /// <returns>This settings.</returns>
        public static MSBuildSettings AddVersionArguments( this MSBuildSettings @this, ICommitBuildInfo info, Action<MSBuildSettings> conf = null )
        {
            AddVersionToolArguments( @this, info );
            conf?.Invoke( @this );
            return @this;
        }

        static void AddVersionToolArguments( Cake.Core.Tooling.ToolSettings t, ICommitBuildInfo info )
        {
            var prev = t.ArgumentCustomization;
            t.ArgumentCustomization = args => (prev?.Invoke( args ) ?? args)
                            .Append( $@"/p:CakeBuild=""true""" )
                            .Append( $@"/p:Version=""{info.Version}""" )
                            .Append( $@"/p:AssemblyVersion=""{info.AssemblyVersion}""" )
                            .Append( $@"/p:FileVersion=""{info.FileVersion}""" )
                            .Append( $@"/p:InformationalVersion=""{info.InformationalVersion}""" );
        }

    }


}
