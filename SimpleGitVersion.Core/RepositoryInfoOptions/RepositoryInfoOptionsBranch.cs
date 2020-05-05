using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace SimpleGitVersion
{
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
        /// <param name="versionName">The optional name that supersedes the branc name.</param>
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
            Name = (string?)e.Attribute( SGVSchema.Name ) ?? (string?)e.Attribute( OldXmlSchema.Name ) ?? throw new XmlException( "Attribute Name is required." ); 
            VersionName = (string?)e.Attribute( SGVSchema.VersionName ) ?? (string?)e.Attribute( OldXmlSchema.VersionName ); 
            var a = e.Attribute( SGVSchema.CIVersionMode ) ?? e.Attribute( OldXmlSchema.CIVersionMode );
            CIBranchVersionMode mode;
            if( a != null && Enum.TryParse<CIBranchVersionMode>( a.Value, true, out mode ) ) 
            {
                CIVersionMode = mode;
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
                                    VersionName != null ? new XAttribute( SGVSchema.VersionName, VersionName ) : null
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
    }
}
