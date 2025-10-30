using CSemVer;

namespace SimpleGitVersion;

/// <summary>
/// Defines <see cref="RepositoryInfoOptions.UseReleaseBuildConfigurationFrom"/>
/// and <see cref="RepositoryInfoOptionsBranch.UseReleaseBuildConfigurationFrom"/> values.
/// </summary>
public enum MinPackageQuality
{
    /// <summary>
    /// Always use "Release" build configuration.
    /// </summary>
    CI = PackageQuality.CI,

    /// <summary>
    /// Always use "Release" except for CI builds.
    /// </summary>
    Exploratory = PackageQuality.Exploratory,

    /// <summary>
    /// Use "Debug" for CI and Exploratory qualities, "Release" otherwise.
    /// </summary>
    Preview = PackageQuality.Preview,

    /// <summary>
    /// This is the default.
    /// </summary>
    ReleaseCandidate = PackageQuality.ReleaseCandidate,

    /// <summary>
    /// Only stable versions will use "Release", all prerelease versions will use "Debug".
    /// </summary>
    Stable = PackageQuality.Stable,

    /// <summary>
    /// Always use "Debug" build configuration.
    /// </summary>
    None = PackageQuality.Stable + 1
}
