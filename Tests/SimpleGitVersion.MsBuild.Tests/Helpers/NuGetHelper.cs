using CK.Core;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SimpleGitVersion.MSBuild.Tests;

/// <summary>
/// NuGet related helpers.
/// </summary>
public static class NuGetHelper
{
    /// <summary>
    /// Removes a package instance or all versions of a package from NuGet global cache.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="packageId">The package name.</param>
    /// <param name="version">Versio to remove or null to remove all versions.</param>
    /// <returns>True on sucess, false if an error occurred while deleting the cached folder.</returns>
    public static bool ClearGlobalCache( IActivityMonitor monitor, string packageId, string? version )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( packageId );
        NormalizedPath p = Environment.GetFolderPath( Environment.SpecialFolder.UserProfile );
        p = p.Combine( ".nuget/packages" ).AppendPart( packageId.ToLowerInvariant() );
        if( version != null ) p = p.AppendPart( version );
        if( Directory.Exists( p ) )
        {
            monitor.Trace( version != null
                            ? $"Removing package '{packageId}@{version}' from NuGet global cache."
                            : $"Removing all versions of package '{packageId}' from NuGet global cache." );

            return FileHelper.DeleteFolder( monitor, p );
        }
        return true;
    }

    public static void EnsureNuGetConfigFile( NormalizedPath solutionPath )
    {
        Throw.CheckArgument( Directory.Exists( solutionPath.AppendPart( ".git" ) ) );
        var f = solutionPath.AppendPart( "nuget.config" );
        if( !File.Exists( f ) )
        {
            File.WriteAllText( f, """
                    <configuration>
                      <packageSources>
                        <clear />

                        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                      </packageSources>

                    </configuration>

                    """ );    
        }

    }

    /// <summary>
    /// Helper that removes a NuGet source or (re)configures it.
    /// When set, the source is moved to the first position in both &lt;packageSources&gt; and &lt;packageSourceMapping&gt;.
    /// See <see href="https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping#enable-by-manually-editing-nugetconfig"/>. 
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="nugetConfigFile">The configuration xml file.</param>
    /// <param name="name">The name of the source.</param>
    /// <param name="sourceUrl">The source url. Null to remove it.</param>
    /// <param name="patterns">Optional patterns. When empty, "*" is used.</param>
    /// <returns>True on success, false otheriwise.</returns>
    public static bool SetOrRemoveNuGetSource( IActivityMonitor monitor,
                                               XDocument nugetConfigFile,
                                               string name,
                                               string? sourceUrl,
                                               params string[] patterns )
    {
        if( nugetConfigFile.Root?.Name.LocalName != "configuration" )
        {
            monitor.Error( $"Missing <configuration> root element in:{Environment.NewLine}{nugetConfigFile}" );
            return false;
        }
        var root = nugetConfigFile.Root;
        var packageSources = root.Elements( "packageSources" ).FirstOrDefault();
        if( packageSources == null )
        {
            monitor.Error( $"Unable to find <packageSources> element in:{Environment.NewLine}{nugetConfigFile}" );
            return false;
        }
        // Fix missing mapping first.
        var mappings = root.Elements( "packageSourceMapping" ).FirstOrDefault();
        if( mappings == null )
        {
            mappings = new XElement( "packageSourceMapping",
                                     packageSources.Elements( "add" ).Attributes( "key" )
                                        .Select( k => new XElement( "packageSource", new XAttribute( "key", k ),
                                                        new XElement( "package", new XAttribute( "pattern", "*" ) ) ) ) );
            root.Add( mappings );
            monitor.Trace( $"Missing <packageSourceMapping>, it is now fixed:{Environment.NewLine}{nugetConfigFile}" );
        }
        // First, removes.
        var existing = packageSources.Elements( "add" ).FirstOrDefault( e => StringComparer.OrdinalIgnoreCase.Equals( name, (string?)e.Attribute( "key" ) ) );
        if( existing != null )
        {
            existing.Remove();
            existing = mappings.Elements( "packageSource" ).FirstOrDefault( e => StringComparer.OrdinalIgnoreCase.Equals( name, (string?)e.Attribute( "key" ) ) );
            existing?.Remove();
        }
        if( sourceUrl != null )
        {
            // Adds the source itself... but not before the </clear> elements!
            var newOne = new XElement( "add", new XAttribute( "key", name ), new XAttribute( "value", sourceUrl ) );
            var clear = packageSources.Elements( "clear" ).LastOrDefault();
            if( clear != null )
            {
                clear.AddAfterSelf( newOne );
            }
            else
            {
                packageSources.AddFirst( newOne );
            }
            // And its mappings in first position.
            if( patterns.Length == 0 ) patterns = ["*"];
            mappings.AddFirst( new XElement( "packageSource", new XAttribute( "key", name ),
                                    patterns.Select( p => new XElement( "package", new XAttribute( "pattern", p ) ) ) ) );
        }
        return true;
    }
}
