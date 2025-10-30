using System.Xml.Linq;

namespace SimpleGitVersion;

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
    public static readonly XName UseReleaseBuildConfigurationFrom = XNamespace.None + "UseReleaseBuildConfigurationFrom";
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
