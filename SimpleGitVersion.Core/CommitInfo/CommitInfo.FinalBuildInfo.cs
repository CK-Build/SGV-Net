using System;
using System.Diagnostics;
using CSemVer;

namespace SimpleGitVersion
{

    public partial class CommitInfo
    {
        ICommitBuildInfo? _commitBuildInfo;

        /// <summary>
        /// Central strategy that choose a build configuration (typically "Debug" or "Release") based on a <see cref="SVersion"/>
        /// and a <see cref="RepositoryInfoOptions"/>.
        /// Defaults to <see cref="DefaultBuildConfigurationSelector(SVersion, RepositoryInfoOptions)"/>.
        /// </summary>
        public static Func<SVersion, RepositoryInfoOptions, string> BuildConfigurationSelector = DefaultBuildConfigurationSelector;

        /// <summary>
        /// Default implementation of <see cref="BuildConfigurationSelector"/>: "Debug" for every one, but "Release" for stable release
        /// (no <see cref="SVersion.Prerelease"/>) and for <see cref="CSVersion"/> with <see cref="CSVersion.PrereleaseName"/> "rc".
        /// </summary>
        /// <param name="finalVersion">The version.</param>
        /// <param name="options">The options to consider (currently unused by this implementation).</param>
        /// <returns>The build configuration to use.</returns>
        public static string DefaultBuildConfigurationSelector( SVersion finalVersion, RepositoryInfoOptions options )
        {
            return finalVersion.Prerelease.Length == 0 || finalVersion.AsCSVersion?.PrereleaseName == "rc"
                                       ? "Release"
                                       : "Debug";
        }

        class RepoCommitBuildInfo : ICommitBuildInfo
        {
            readonly CommitInfo _info;

            public RepoCommitBuildInfo( CommitInfo info )
            {
                Debug.Assert( info != null );
                _info = info;
                
                BuildConfiguration = BuildConfigurationSelector( info.FinalVersion, info.Options );
                AssemblyVersion = $"{info.FinalVersion.Major}.{info.FinalVersion.Minor}";
                if( info.Error == null )
                {
                    if( info.CIRelease != null )
                    {
                        if( info.CIRelease.IsZeroTimed )
                        {
                            FileVersion = CSemVer.InformationalVersion.ZeroFileVersion;
                        }
                        else
                        {
                            Debug.Assert( info.CIRelease.BaseTag.AsCSVersion != null, "In LastReleaseBased mode, there is always a valid CSVersion base tag." );
                            FileVersion = info.CIRelease.BaseTag.AsCSVersion.ToStringFileVersion( true );
                        }
                    }
                    else 
                    {
                        Debug.Assert( info.ReleaseTag != null );
                        FileVersion = info.ReleaseTag.ToStringFileVersion( false );
                    }
                }
                else
                {
                    FileVersion = CSemVer.InformationalVersion.ZeroFileVersion;
                }
            }

            public string BuildConfiguration { get; }

            public string CommitSha => _info.CommitSha ?? CSemVer.InformationalVersion.ZeroCommitSha;

            public DateTime CommitDateUtc => _info.CommitDateUtc;

            public string InformationalVersion => _info.FinalInformationalVersion;

            public SVersion Version => _info.FinalVersion;

            public string AssemblyVersion { get; }

            public string FileVersion { get; }
        }

        /// <summary>
        /// Gets the final build information to use.
        /// </summary>
        public ICommitBuildInfo FinalBuildInfo => _commitBuildInfo ??= new RepoCommitBuildInfo( this );
    }
}
