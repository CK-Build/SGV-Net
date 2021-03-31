using System;
using System.Diagnostics;
using CSemVer;

namespace SimpleGitVersion
{

    public partial class CommitInfo
    {
        ICommitBuildInfo? _commitBuildInfo;

        /// <summary>
        /// Defines the <see cref="BuildConfigurationSelector"/> signature.
        /// This is required since the <see cref="Func{T1, T2, TResult}"/> cannot accept "in" parameters. 
        /// </summary>
        /// <param name="commitInfo">The initial commit info that also exposes the configurations.</param>
        /// <param name="finalVersion">The version for which a build configuration must be computed.</param>
        /// <returns>The build configuration to use.</returns>
        public delegate string BuildConfigurationDelegate( in InitialInfo commitInfo, SVersion finalVersion );

        /// <summary>
        /// Central strategy that choose a build configuration (typically "Debug" or "Release") based on the <see cref="InitialInfo"/>
        /// and a <see cref="SVersion"/>.
        /// Defaults to <see cref="DefaultBuildConfigurationSelector(in InitialInfo, SVersion)"/>.
        /// </summary>
        public static BuildConfigurationDelegate BuildConfigurationSelector = DefaultBuildConfigurationSelector;

        /// <summary>
        /// Default implementation of <see cref="BuildConfigurationSelector"/> that relies on <see cref="RepositoryInfoOptions.UseReleaseBuildConfigurationFrom"/>
        /// and <see cref="RepositoryInfoOptionsBranch.UseReleaseBuildConfigurationFrom"/> configurations.
        /// </summary>
        /// <param name="commitInfo">The initial commit info that also exposes the configurations.</param>
        /// <param name="finalVersion">The version for which a build configuration must be computed.</param>
        /// <returns>The build configuration to use.</returns>
        public static string DefaultBuildConfigurationSelector( in InitialInfo commitInfo, SVersion finalVersion )
        {
            // Look for the RepositoryInfoOptionsBranch first.
            PackageQuality q = commitInfo.FoundBranchOption?.UseReleaseBuildConfigurationFrom ?? commitInfo.Options.UseReleaseBuildConfigurationFrom;
            // None means "Always Debug", "CI" means always "Release".
            return q == PackageQuality.None || finalVersion.PackageQuality < q ? "Debug" : "Release";
        }

        class RepoCommitBuildInfo : ICommitBuildInfo
        {
            readonly CommitInfo _info;

            public RepoCommitBuildInfo( CommitInfo info )
            {
                Debug.Assert( info != null );
                _info = info;
                
                BuildConfiguration = BuildConfigurationSelector( info.StartingCommit, info.FinalVersion );
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
