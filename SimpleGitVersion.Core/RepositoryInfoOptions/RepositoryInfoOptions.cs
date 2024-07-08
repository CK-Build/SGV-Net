using CSemVer;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SimpleGitVersion
{
    /// <summary>
    /// Describes options for initializing <see cref="CommitInfo"/>.
    /// </summary>
    public class RepositoryInfoOptions
    {
        string? _remoteName;

        /// <summary>
        /// Initializes a new <see cref="RepositoryInfoOptions"/>.
        /// </summary>
        public RepositoryInfoOptions()
        {
            UseReleaseBuildConfigurationFrom = PackageQuality.ReleaseCandidate;
            IgnoreModifiedFiles = new HashSet<string>( PathComparer.Default );
            Branches = new List<RepositoryInfoOptionsBranch>();
        }

        /// <summary>
        /// Initializes a new <see cref="RepositoryInfoOptions"/> from its Xml representation.
        /// The element must be named <see cref="SGVSchema.SimpleGitVersion"/> or has a child
        /// element that is named SimpleGitVersion.
        /// </summary>
        /// <param name="e">The SimpleGitVersion XElement or its direct parent.</param>
        public RepositoryInfoOptions( XElement e )
            : this()
        {
            var sgv = e.Attributes().Any( a => a.Value == OldXmlSchema.SVGNS )
                        ? null
                        : (e.Name == SGVSchema.SimpleGitVersion
                            ? e
                            : e.Element( SGVSchema.SimpleGitVersion) );
            if( sgv != null )
            {
                IgnoreDirtyWorkingFolder = (bool?)sgv.Element( SGVSchema.Debug )?.Attribute( SGVSchema.IgnoreDirtyWorkingFolder ) ?? false;
                IgnoreAlreadyExistingVersion = (bool?)sgv.Attribute( SGVSchema.IgnoreAlreadyExistingVersion ) ?? false;
                CheckExistingVersions = (bool?)sgv.Attribute( SGVSchema.CheckExistingVersions ) ?? false;
                StartingVersion = (string?)sgv.Attribute( SGVSchema.StartingVersion );
                SingleMajor = (int?)sgv.Attribute( SGVSchema.SingleMajor );
                OnlyPatch = (bool?)sgv.Attribute( SGVSchema.OnlyPatch ) ?? false;

                var s = (string?)sgv.Attribute( SGVSchema.UseReleaseBuildConfigurationFrom );
                if( s != null )
                {
                    UseReleaseBuildConfigurationFrom = ParsePackageQualityOrThrow( s, true );
                }
                else
                {
                    UseReleaseBuildConfigurationFrom = PackageQuality.ReleaseCandidate;
                }

                Branches.AddRange( sgv.Elements( SGVSchema.Branches )
                                      .Elements( SGVSchema.Branch )
                                      .Select( b => new RepositoryInfoOptionsBranch( b ) ) );
                IgnoreModifiedFiles.UnionWith( sgv.Elements( SGVSchema.IgnoreModifiedFiles ).Elements( SGVSchema.Add ).Select( i => i.Value ) );
                RemoteName = (string?)sgv.Attribute( SGVSchema.RemoteName );
            }
            else
            {
                XmlMigrationRequired = true;

                IgnoreDirtyWorkingFolder = (bool?)e.Element( OldXmlSchema.Debug )?.Attribute( OldXmlSchema.IgnoreDirtyWorkingFolder ) ?? false;
                StartingVersion = (string?)e.Element( OldXmlSchema.StartingVersionForCSemVer );
                SingleMajor = (int?)e.Element( OldXmlSchema.SingleMajor );
                OnlyPatch = (bool?)e.Element( OldXmlSchema.OnlyPatch ) ?? false;
                Branches.AddRange( e.Elements( OldXmlSchema.Branches )
                                    .Elements( OldXmlSchema.Branch )
                                    .Select( b => new RepositoryInfoOptionsBranch( b ) ) );
                IgnoreModifiedFiles.UnionWith( e.Elements( OldXmlSchema.IgnoreModifiedFiles ).Elements( OldXmlSchema.Add ).Select( i => i.Value ) );
                RemoteName = (string?)e.Element( OldXmlSchema.RemoteName );
            }
        }

        internal static PackageQuality ParsePackageQualityOrThrow( string s, bool rcIsDefault )
        {
            if( !PackageQualityExtension.TryMatch( s.Trim(), out var q ) )
            {
                var msg = $"Invalid UseReleaseBuildConfigurationFrom attribute value '{s}'. "
                        + $"When specified, it must be: '{nameof( PackageQuality.None )}' (always use \"Debug\" build configuration), "
                        + $"'{nameof( PackageQuality.CI )}' (always use \"Release\" build configuration), "
                        + $"'{nameof( PackageQuality.Exploratory )}', "
                        + $"'{nameof( PackageQuality.Preview )}', "
                        + $"'{nameof( PackageQuality.ReleaseCandidate )}' or 'rc'{(rcIsDefault ? " (this is the default)" : "")}, "
                        + $"or '{nameof( PackageQuality.Stable )}' (only stable versions will use \"Release\").";
                throw new XmlException( msg );
            }
            return q;
        }

        /// <summary>
        /// Gets this options as an Xml element named <see cref="SGVSchema.SimpleGitVersion"/>.
        /// </summary>
        /// <returns>The SimpleGitVersion XElement.</returns>
        public XElement ToXml()
        {
            return new XElement( SGVSchema.SimpleGitVersion,
                                    IgnoreDirtyWorkingFolder
                                        ? new XElement( SGVSchema.Debug, new XAttribute( SGVSchema.IgnoreDirtyWorkingFolder, "true" ) )
                                        : null,
                                    IgnoreAlreadyExistingVersion
                                        ? new XAttribute( SGVSchema.IgnoreAlreadyExistingVersion, "true" )
                                        : null,
                                    CheckExistingVersions
                                        ? new XAttribute( SGVSchema.CheckExistingVersions, "true" )
                                        : null,
                                    StartingVersion != null
                                        ? new XAttribute( SGVSchema.StartingVersion, StartingVersion )
                                        : null,
                                    SingleMajor.HasValue
                                        ? new XAttribute( SGVSchema.SingleMajor, SingleMajor.Value.ToString() )
                                        : null,
                                    OnlyPatch
                                        ? new XAttribute( SGVSchema.OnlyPatch, "true" )
                                        : null,
                                    UseReleaseBuildConfigurationFrom != PackageQuality.ReleaseCandidate
                                        ? new XAttribute( SGVSchema.UseReleaseBuildConfigurationFrom, UseReleaseBuildConfigurationFrom )
                                        : null,
                                    IgnoreModifiedFiles.Count > 0
                                        ? new XElement( SGVSchema.IgnoreModifiedFiles,
                                                            IgnoreModifiedFiles.Where( f => !string.IsNullOrWhiteSpace( f ) ).Select( f => new XElement( SGVSchema.Add, f ) ) )
                                        : null,
                                    Branches != null
                                        ? new XElement( SGVSchema.Branches,
                                                            Branches.Where( b => b != null ).Select( b => b.ToXml() ) )
                                        : null,
                                    RemoteName != "origin" ? new XAttribute( SGVSchema.RemoteName, RemoteName ) : null );
        }

        /// <summary>
        /// Gets whether the old xml schema has been detected.
        /// </summary>
        public bool XmlMigrationRequired { get; }

        /// <summary>
        /// Gets or sets the commit that will be analyzed: this is a "revparse spec" (commit sha, tag name, local or remote/branch, etc.)
        /// that should ultimately resolve to a commit.
        /// When null (the default) or empty, the <see cref="HeadBranchName"/> is used.
        /// This property must be used programmatically: it does not appear in the Xml file.
        /// </summary>
        public string? HeadCommit { get; set; }

        /// <summary>
        /// Gets or sets the branch whose name will be analyzed. Applies only when <see cref="HeadCommit"/> is null or empty.
        /// When null (the default) or empty, the current head is used.
        /// This property must be used programmatically: it does not appear in the Xml file.
        /// </summary>
        public string? HeadBranchName { get; set; }

        /// <summary>
        /// Gets or sets an enumerable of commits' sha with tags. Defaults to null.
        /// All commit sha MUST exist in the repository otherwise an error will be added to the error collector.
        /// If the key is "head" (instead of a SHA1) the tags are applied on the current head of the repository.
        /// These tags are applied as if they exist in the repository.
        /// This property must be used programmatically: it does not appear in the Xml file.
        /// </summary>
        /// <remarks>
        /// A dictionary of string to list of sting can be directly assigned to this property.
        /// </remarks>
        public IEnumerable<KeyValuePair<string, IReadOnlyList<string>>>? OverriddenTags { get; set; }

        /// <summary>
        /// Gets or sets a version from which CSemVer rules are enforced.
        /// When set, any version before this one are silently ignored.
        /// This is useful to accommodate an existing repository that did not use Simple Git Versioning by easily forgetting the past.
        /// Xml activation: <code>&lt;SimpleGitVersion StartingVersion="v4.3.0" /&gt;</code>
        /// </summary>
        public string? StartingVersion { get; set; }

        /// <summary>
        /// Gets or sets the only major version that must be released.
        /// This is typically for LTS branches.
        /// Obviously defaults to null.
        /// Xml activation: <code>&lt;SimpleGitVersion SingleMajor="2" /&gt;</code>
        /// </summary>
        public int? SingleMajor { get; set; }

        /// <summary>
        /// Gets or sets the whether only patch versions must be released.
        /// This is typically for LTS (fix only) branches. 
        /// Xml activation: <code>&lt;SimpleGitVersion OnlyPatch="true" /&gt;</code>
        /// </summary>
        public bool OnlyPatch { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="PackageQuality"/> from which "Release" build configuration (<see cref="ICommitBuildInfo.BuildConfiguration"/>)
        /// will be used instead of "Debug".
        ///<para>
        /// Defaults to "rc" (<see cref="PackageQuality.ReleaseCandidate"/>):
        /// only <see cref="PackageQuality.Stable"/> and <see cref="PackageQuality.ReleaseCandidate"/> will
        /// be use "Release", the others will use "Debug".
        /// </para>
        /// <para>
        /// When <see cref="PackageQuality.None"/> is specified, "Debug" build configuration will always be used.
        /// </para>
        /// <para>
        /// The same property can be set at the branch level and overrides this one (<see cref="RepositoryInfoOptionsBranch.UseReleaseBuildConfigurationFrom"/>).
        /// </para>
        /// </summary>
        public PackageQuality UseReleaseBuildConfigurationFrom { get; set; }

        /// <summary>
        /// Gets or sets branches informations.
        /// Xml example:
        /// <code>
        /// &lt;SimpleGitVersion&gt;
        ///     &lt;Branches&gt;
        ///         &lt;Branch Name="develop" CIVersionMode="LastReleaseBased" VersionName="dev" /&gt;
        ///     &lt;/Branches&gt;
        /// &lt;/SimpleGitVersion&gt;
        /// </code>
        /// </summary>
        public List<RepositoryInfoOptionsBranch> Branches { get; }

        /// <summary>
        /// Gets a set of paths for which local modifications are ignored.
        /// It is empty by default.
        /// Xml example:
        /// <code>
        /// &lt;SimpleGitVersion&gt;
        ///     &lt;IgnoreModifiedFiles&gt;
        ///         &lt;Add&gt;File1.txt&lt;/Add&gt;
        ///         &lt;Add&gt;Common/File2.exe&lt;/Add&gt;
        ///     &lt;/IgnoreModifiedFiles&gt;
        /// &lt;/SimpleGitVersion&gt;
        /// </code>
        /// </summary>
        public ISet<string> IgnoreModifiedFiles { get; }

        /// <summary>
        /// Gets or sets a filter for modified file: when null, all <see cref="IWorkingFolderModifiedFile"/>
        /// are considered modified (as if this predicate always evaluates to false).
        /// This hook is called only if the file does not appear in <see cref="IgnoreModifiedFiles"/>.
        /// </summary>
        /// <value>The file filter.</value>
        public Func<IWorkingFolderModifiedFile, bool>? IgnoreModifiedFilePredicate { get; set; }

        /// <summary>
        /// Gets or sets whether all modified files must be processed: when false (the default), as soon as the first
        /// modified file is found (not in the <see cref="IgnoreModifiedFiles"/> and <see cref="IgnoreModifiedFilePredicate"/> returned 
        /// false) the process stops.
        /// </summary>
        public bool IgnoreModifiedFileFullProcess { get; set; }

        /// <summary>
        /// Gets or sets the name of the remote repository that will be considered when
        /// working with branches. Defaults to "origin" (can never be null or empty).
        /// Xml activation: <code>&lt;SimpleGitVersion RemoteName="another-origin" /&gt;</code>
        /// </summary>
        [AllowNull]
        public string RemoteName
        {
            get => _remoteName ?? "origin";
            set
            {
                _remoteName = string.IsNullOrWhiteSpace( value ) ? null : value;
            }
        }

        /// <summary>
        /// Gets or sets whether the <see cref="CommitInfo.IsDirty"/> is ignored.
        /// This should be used only for debugging purposes.
        /// Xml activation: <code>&lt;SimpleGitVersion&gt; &lt;Debug IgnoreDirtyWorkingFolder="true" /&gt; &lt;/SimpleGitVersion&gt;</code>
        /// </summary>
        public bool IgnoreDirtyWorkingFolder { get; set; }

        /// <summary>
        /// Gets or sets whether a <see cref="ICommitInfo.AlreadyExistingVersion"/> must be ignored.
        /// Xml activation: <code>&lt;SimpleGitVersion IgnoreAlreadyExistingVersion="true" /&gt;</code>
        /// </summary>
        public bool IgnoreAlreadyExistingVersion { get; set; }

        /// <summary>
        /// Gets or sets whether existing versions should be checked. See <see cref="CommitInfo.ExistingVersions"/>.
        /// Xml activation: <code>&lt;SimpleGitVersion CheckExistingVersions="true" /&gt;</code>
        /// </summary>
        public bool CheckExistingVersions { get; set; }

        /// <summary>
        /// Reads <see cref="RepositoryInfoOptions"/> from a xml file.
        /// </summary>
        /// <param name="existingFilePath">Path to a xml file.</param>
        /// <returns>Returns a configured <see cref="RepositoryInfoOptions"/>.</returns>
        public static RepositoryInfoOptions Read( string existingFilePath )
        {
            return new RepositoryInfoOptions( XDocument.Load( existingFilePath ).Root! );
        }

    }
}
