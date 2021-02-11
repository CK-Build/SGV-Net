using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SimpleGitVersion
{
    static class OldXmlSchema
    {

        public static readonly XNamespace SVGNS = XNamespace.Get( "http://csemver.org/schemas/2015" );
        public static readonly XName RepositoryInfo = SVGNS + "RepositoryInfo";
        public static readonly XName StartingVersionForCSemVer = SVGNS + "StartingVersionForCSemVer";
        public static readonly XName IgnoreModifiedFiles = SVGNS + "IgnoreModifiedFiles";
        public static readonly XName SingleMajor = SVGNS + "SingleMajor";
        public static readonly XName OnlyPatch = SVGNS + "OnlyPatch";
        public static readonly XName Add = SVGNS + "Add";
        public static readonly XName Debug = SVGNS + "Debug";
        public static readonly XName Branches = SVGNS + "Branches";
        public static readonly XName Branch = SVGNS + "Branch";
        public static readonly XName RemoteName = SVGNS + "RemoteName";

        public static readonly XName SimpleGitVersion = SVGNS + "SimpleGitVersion";

        public static readonly XName Name = XNamespace.None + "Name";
        public static readonly XName CIVersionMode = XNamespace.None + "CIVersionMode";
        public static readonly XName VersionName = XNamespace.None + "VersionName";
        public static readonly XName IgnoreDirtyWorkingFolder = XNamespace.None + "IgnoreDirtyWorkingFolder";
    }

    /// <summary>
    /// Exposes all names used in Xml configuration file as static readonly <see cref="XName"/> in
    /// the <see cref="XNamespace.None"/>.
    /// </summary>
    public static class SGVSchema
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public static readonly XName SimpleGitVersion = XNamespace.None + "SimpleGitVersion";
        public static readonly XName StartingVersion = XNamespace.None + "StartingVersion";
        public static readonly XName IgnoreModifiedFiles = XNamespace.None + "IgnoreModifiedFiles";
        public static readonly XName SingleMajor = XNamespace.None + "SingleMajor";
        public static readonly XName OnlyPatch = XNamespace.None + "OnlyPatch";
        public static readonly XName Add = XNamespace.None + "Add";
        public static readonly XName Debug = XNamespace.None + "Debug";
        public static readonly XName Branches = XNamespace.None + "Branches";
        public static readonly XName Branch = XNamespace.None + "Branch";
        public static readonly XName RemoteName = XNamespace.None + "RemoteName";

        public static readonly XName Name = XNamespace.None + "Name";
        public static readonly XName CIVersionMode = XNamespace.None + "CIVersionMode";
        public static readonly XName VersionName = XNamespace.None + "VersionName";
        public static readonly XName IgnoreDirtyWorkingFolder = XNamespace.None + "IgnoreDirtyWorkingFolder";
        public static readonly XName IgnoreAlreadyExistingVersion = XNamespace.None + "IgnoreAlreadyExistingVersion";
        public static readonly XName CheckExistingVersions = XNamespace.None + "CheckExistingVersions";

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
