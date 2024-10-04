using CSemVer;
using System;
using System.Xml;
using System.Xml.Linq;

namespace SimpleGitVersion;

/// <summary>
/// Describes options related to a Git branch.
/// </summary>
public class RepositoryInfoOptionsBranch
{
    /// <summary>
    /// Initializes a new default <see cref="RepositoryInfoOptionsBranch"/> object.
    /// </summary>
    /// <param name="name">Required <see cref="Name"/> of this branch.</param>
    /// <param name="mode">The CI version mode to use.</param>
    /// <param name="versionName">The optional name that supersedes the branch name.</param>
    public RepositoryInfoOptionsBranch( string name, CIBranchVersionMode mode, string? versionName = null )
    {
        Name = name;
        CIVersionMode = mode;
        VersionName = versionName;
    }

    /// <summary>
    /// Initializes a new branch information from a <see cref="XElement"/>.
    /// </summary>
    /// <param name="e">The xml element.</param>
    public RepositoryInfoOptionsBranch( XElement e )
    {
        Name = (string?)e.Attribute( SGVSchema.Name ) ?? (string?)e.Attribute( OldXmlSchema.Name ) ?? throw new XmlException( "Attribute Name is required on the Branch element." );
        VersionName = (string?)e.Attribute( SGVSchema.VersionName ) ?? (string?)e.Attribute( OldXmlSchema.VersionName );

        var a = e.Attribute( SGVSchema.CIVersionMode ) ?? e.Attribute( OldXmlSchema.CIVersionMode );
        if( a != null )
        {
            if( !Enum.TryParse( a.Value, true, out CIBranchVersionMode mode ) )
            {
                throw new XmlException( $"Invalid CIVersionMode attribute value '{a.Value}'. It must be '{nameof( CIBranchVersionMode.None )}', '{nameof( CIBranchVersionMode.ZeroTimed )}' or '{CIBranchVersionMode.LastReleaseBased}'." );
            }
            CIVersionMode = mode;
        }

        var s = (string?)e.Attribute( SGVSchema.UseReleaseBuildConfigurationFrom );
        if( s != null )
        {
            PackageQuality q = RepositoryInfoOptions.ParsePackageQualityOrThrow( s, false );
            UseReleaseBuildConfigurationFrom = q;
        }

    }

    /// <summary>
    /// Gets this branch as an Xml element.
    /// </summary>
    /// <returns>The XElement.</returns>
    public XElement ToXml()
    {
        return new XElement( SGVSchema.Branch,
                                new XAttribute( SGVSchema.Name, Name ),
                                new XAttribute( SGVSchema.CIVersionMode, CIVersionMode.ToString() ),
                                VersionName != null
                                    ? new XAttribute( SGVSchema.VersionName, VersionName )
                                    : null,
                                UseReleaseBuildConfigurationFrom != null
                                    ? new XAttribute( SGVSchema.UseReleaseBuildConfigurationFrom, UseReleaseBuildConfigurationFrom )
                                    : null
                           );
    }


    /// <summary>
    /// Gets or sets the name of the branch.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional name that will be used instead of <see cref="Name"/> in the version.
    /// </summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Gets or sets the wanted behavior for this branch.
    /// </summary>
    public CIBranchVersionMode CIVersionMode { get; set; }

    /// <summary>
    /// Gets or sets a <see cref="PackageQuality"/> from which "Release" build configuration (<see cref="ICommitBuildInfo.BuildConfiguration"/>)
    /// will be used instead of "Debug".
    /// <para>
    /// Defaults to null.
    /// When not null, this overrides the <see cref="RepositoryInfoOptions.UseReleaseBuildConfigurationFrom"/> value.
    /// </para>
    /// </summary>
    public PackageQuality? UseReleaseBuildConfigurationFrom { get; set; }

}
