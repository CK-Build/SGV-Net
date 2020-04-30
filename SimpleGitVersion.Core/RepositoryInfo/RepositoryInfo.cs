using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using System.IO;
using CSemVer;

namespace SimpleGitVersion
{

    /// <summary>
    /// Immutable object that describes the commit and all the CSemVer information.
    /// It can be obtained by calling static helper <see cref="LoadFromPath(string, RepositoryInfoOptions)"/>
    /// (a <see cref="Repository"/> is created and disposed) or by using its constructor.
    /// </summary>
    public partial class RepositoryInfo : IRepositoryInfo
    {
        /// <summary>
        /// Gets the solution directory: the one that contains the .git folder.
        /// Null only if <see cref="Error"/> is 'No Git repository.'.
        /// It ends with the <see cref="Path.DirectorySeparatorChar"/>.
        /// </summary>
        /// <remarks>
        /// This captures the <see cref="RepositoryInformation.WorkingDirectory"/>.
        /// </remarks>
        public readonly string? GitSolutionDirectory;

        /// <summary>
        /// Gets the initial commit analysis result.
        /// Its <see cref="StartingCommitInfo.Error"/> holds the primary error that may prevent any further analysis.
        /// </summary>
        public readonly StartingCommitInfo StartingCommit;

        /// <summary>
        /// Gets either the <see cref="StartingCommit"/>'s error or any error that occurred during
        /// the subsequent analysis.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets whether there are non committed files in the working directory.
        /// This may or may be 
        /// </summary>
        public bool IsDirty => IsDirtyExplanations != null;

        /// <summary>
        /// Gets detailed explanations about <see cref="IsDirty"/>.
        /// </summary>
        public readonly string? IsDirtyExplanations;

        /// <summary>
        /// Gets the existing versions in the repository in ascending order, filtered by <see cref="RepositoryInfoOptions.SingleMajor"/> if it is defined.
        /// Null if an <see cref="Error"/> prevented its computation.
        /// <para>
        /// Unicity of tags is always checked: version polymorphisms are considered (optional 'v' prefix, short/long forms and similar normalizations).
        /// </para>
        /// <para>
        /// If checking existing versions have been asked then:
        /// <list type="bullet">
        ///   <item>If there is no <see cref="StartingVersionForCSemVer"/>, the first version must be one of the <see cref="CSVersion.FirstPossibleVersions"/>.</item>
        ///   <item>If there is a <see cref="StartingVersionForCSemVer"/>, the first version must be this starting one.</item>
        ///   <item>Existing versions must always be compact (ie. no "holes" must exist between them).</item>
        /// </list>
        /// </para>
        /// </summary>
        public readonly IReadOnlyList<ITagCommit>? ExistingVersions;

        /// <inheritdoc/>
        public ITagCommit? BetterExistingVersion { get; }

        /// <inheritdoc/>
        public ITagCommit? BestCommitBelow { get; }

        /// <inheritdoc/>
        public CSVersion? ValidReleaseTag { get; }

        /// <summary>
        /// Gets the <see cref="SimpleGitVersion.CommitInfo"/> of the current commit point.
        /// Null if there is a <see cref="Error"/> that prevented its computation.
        /// </summary>
        public readonly CommitInfo? CommitInfo;

        /// <summary>
        /// When empty, this means that there cannot be a valid release tag on the current commit point.
        /// Null when an <see cref="Error"/> prevented its computation.
        /// This is the set of filtered versions (<see cref="RepositoryInfoOptions.SingleMajor"/>
        /// and <see cref="RepositoryInfoOptions.OnlyPatch"/> options are applied
        /// on <see cref="CommitInfo"/>.<see cref="CommitInfo.PossibleVersions">PossibleVersions</see>).
        /// </summary>
        public IReadOnlyList<CSVersion>? PossibleVersions { get; }

        /// <summary>
        /// These are the versions that may be available to any commit above the current one.
        /// Null when a <see cref="StartingCommit"/>'s error or <see cref="CommitAnalyzingError"/> prevented its computation.
        /// This is the set of filtered versions (<see cref="RepositoryInfoOptions.SingleMajor"/>
        /// and <see cref="RepositoryInfoOptions.OnlyPatch"/> options are applied
        /// on <see cref="CommitInfo"/>.<see cref="CommitInfo.NextPossibleVersions">NextPossibleVersions</see>).
        /// Empty if there is a <see cref="Error"/> that prevented its computation.
        /// </summary>
        public IReadOnlyList<CSVersion>? NextPossibleVersions { get; }

        /// <inheritdoc />
        public CIReleaseInfo? CIRelease { get; }

        /// <summary>
        /// Gets the final version (based on short CSVersion form, see <see cref="CSVersion.IsLongForm"/>) that must be used to build this commit point.
        /// Never null: defaults to <see cref="SVersion.ZeroVersion"/>.
        /// </summary>
        public readonly SVersion FinalVersion;

        /// <summary>
        /// Gets the standardized information version string that must be used to build this commit point.
        /// Never null: defaults to <see cref="InformationalVersion.ZeroInformationalVersion"/> string.
        /// </summary>
        public readonly string FinalInformationalVersion;

        /// <summary>
        /// Gets the <see cref="RepositoryInfoOptions"/> that has been used.
        /// </summary>
        public RepositoryInfoOptions Options => StartingCommit.Options;

        /// <summary>
        /// The UTC date and time of the commit.
        /// Defaults to <see cref="InformationalVersion.ZeroCommitDate"/>.
        /// </summary>
        public readonly DateTime CommitDateUtc;

        /// <summary>
        /// The Sha of the commit.
        /// Null if the commit is not valid.
        /// </summary>
        public readonly string? CommitSha;

        /// <summary>
        /// Initializes a new <see cref="RepositoryInfo"/> on a LibGit2Sharp <see cref="Repository"/>.
        /// </summary>
        /// <param name="r">The repository (can be invalid and even null).</param>
        /// <param name="options">Optional options.</param>
        public RepositoryInfo( Repository? r, RepositoryInfoOptions? options = null )
        {
            CommitDateUtc = InformationalVersion.ZeroCommitDate;
            SVersion? finalVersion = null;
            StartingCommit = new StartingCommitInfo( options ??= new RepositoryInfoOptions(), r );
            if( StartingCommit.Error != null )
            {
                Error = StartingCommit.Error;
            }
            else
            {
                Commit? commit = StartingCommit.Commit;
                Debug.Assert( r != null && commit != null, "Since there is no error, there is a repo and a commit." );
                Debug.Assert( (StartingCommit.CIVersionMode != CIBranchVersionMode.None) == (StartingCommit.CIBranchVersionName != null) );
                CommitSha = commit.Sha;
                CommitDateUtc = commit.Author.When.UtcDateTime;
                IsDirtyExplanations = ComputeIsDirty( r, commit, options );

                StringBuilder errors = new StringBuilder();
                if( IsDirty && !options.IgnoreDirtyWorkingFolder )
                {
                    errors.AppendLine( "Working folder has non committed changes." );
                    errors.Append( IsDirtyExplanations );
                }
                else 
                {
                    TagCollector collector = new TagCollector( errors,
                                                               r,
                                                               options.StartingVersionForCSemVer,
                                                               options.OverriddenTags,
                                                               options.SingleMajor );
                    if( errors.Length == 0 )
                    {
                        Debug.Assert( collector.ExistingVersions != null );

                        ExistingVersions = collector.ExistingVersions.TagCommits;
                        CommitInfo info = collector.GetCommitInfo( commit );
                        CommitInfo = info;

                        var rawPossible = info.PossibleVersions;
                        IEnumerable<CSVersion> possibles = rawPossible;
                        if( options.OnlyPatch ) possibles = possibles.Where( v => v.IsPatch );
                        if( options.SingleMajor.HasValue ) possibles = possibles.Where( v => v.Major == options.SingleMajor.Value );
                        PossibleVersions = possibles != rawPossible ? possibles.ToList() : rawPossible;

                        var rawNextPossible = info.NextPossibleVersions;
                        IEnumerable<CSVersion> nextPossibles = rawNextPossible;
                        if( options.OnlyPatch ) nextPossibles = nextPossibles.Where( v => v.IsPatch );
                        if( options.SingleMajor.HasValue ) nextPossibles = nextPossibles.Where( v => v.Major == options.SingleMajor.Value );
                        NextPossibleVersions = nextPossibles != rawNextPossible ? nextPossibles.ToList() : rawNextPossible;

                        ITagCommit? thisReleaseTag = null;
                        if( info.BasicInfo != null )
                        {
                            thisReleaseTag = info.BasicInfo.UnfilteredThisCommit;

                            BestCommitBelow = info.BasicInfo.BestCommitBelow;
                            if( info.BasicInfo.BestCommit?.ThisTag > thisReleaseTag?.ThisTag )
                            {
                                BetterExistingVersion = info.BasicInfo.BestCommit;
                            }
                        }
                        if( thisReleaseTag != null )
                        {
                            if( PossibleVersions.Contains( thisReleaseTag.ThisTag ) )
                            {
                                ValidReleaseTag = thisReleaseTag.ThisTag;
                            }
                            else
                            {
                                errors.Append( "Release tag '" )
                                        .Append( thisReleaseTag.ThisTag )
                                        .AppendLine( "' is not valid here. " );
                                errors.Append( "Valid tags are: " )
                                        .AppendJoin( ", ", PossibleVersions )
                                        .AppendLine();
                                if( PossibleVersions != rawPossible && rawPossible.Contains( thisReleaseTag.ThisTag ) )
                                {
                                    errors.AppendLine( "Note: this version is invalid because of <SingleMajor> or <OnlyPatch> setting in RepositoryInfo.xml." );
                                }
                            }
                        }
                        else
                        {
                            // There is no release tag on the commit point.
                            // Are we on a CI-enabled branch?
                            string? ciBuildName = StartingCommit.CIBranchVersionName;
                            if( ciBuildName != null && ciBuildName.Length <= 8 )
                            {
                                CIRelease = CIReleaseInfo.Create( commit, StartingCommit.CIVersionMode, ciBuildName, info.BasicInfo );
                            }
                            else
                            {
                                errors.Append( "No release tag found and CI build is not possible: " );
                                if( ciBuildName != null )
                                {
                                    errors.AppendLine( "the branch name must not be longer than 8 characters. " )
                                          .AppendLine( "Adds a VersionName attribute to the branch element in RepositoryInfo.xml with a shorter name: " )
                                          .AppendLine( "<Branches>" )
                                          .AppendLine( $@"  <Branch Name=""{ciBuildName}"" VersionName=""{ciBuildName.Substring( 0, 8 )}"" ... />." )
                                          .AppendLine( "</Branches>" );
                                }
                                else if( StartingCommit.ConsideredBranchNames == null )
                                {
                                    errors.AppendLine( $"no CI branches have been considered since an explicit StartingCommitSha = '{options.StartingCommitSha}' has been specified." );
                                }
                                else if( StartingCommit.ConsideredBranchNames.Count == 0 )
                                {
                                    Debug.Assert( options.StartingBranchName == null, "If the StartingBranchName has not been found, it's in StartingCommit.Error." );
                                    errors.AppendLine( "no branches reference the current repository's head commit." );
                                }
                                else if( StartingCommit.FoundBranchOption == null )
                                {
                                    errors.Append( "no CI Branch information defined for " );
                                    if( StartingCommit.ConsideredBranchNames.Count == 1 )
                                    {
                                        errors.Append( "branch '" ).Append( StartingCommit.ConsideredBranchNames.First() );
                                    }
                                    else
                                    {
                                        errors.Append( "any of the branches '" ).AppendJoin( "', '", StartingCommit.ConsideredBranchNames );
                                    }
                                    errors.AppendLine( "'." )
                                          .AppendLine( "Adds a Branch element in RepositoryInfo.xml with the branch name of interest, for instance:" )
                                          .AppendLine( "<Branches>" )
                                          .AppendLine( @"  <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />." )
                                          .AppendLine( @"  <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""explo"" />." )
                                          .AppendLine( @"  <Branch Name=""fx/new-computation"" CIVersionMode=""ZeroTimed"" VersionName=""explo"" />." )
                                          .AppendLine( "</Branches>" );
                                }
                                else
                                {
                                    errors.AppendLine( $@"configured CI branch '{StartingCommit.FoundBranchOption.Name}' found explicitly states that CIVersionMode=""None""." )
                                          .AppendLine( @"Use CIVersionMode=""ZeroTimed"" or CIVersionMode=""LastReleaseBased"" to enable CI build on this branch." );
                                }
                            }
                        }
                    }
                }

                // Conclusion:
                if( ValidReleaseTag != null )
                {
                    Debug.Assert( !ValidReleaseTag.IsLongForm );
                    finalVersion = ValidReleaseTag;
                }
                else if( CIRelease != null )
                {
                    finalVersion = CIRelease.BuildVersion;
                }
                if( errors.Length > 0 ) Error = errors.ToString();
            }

            Debug.Assert( (Error != null) != (finalVersion != null), "If there is an error, then there is a no final version. And vice versa." );
            if( finalVersion == null )
            {
                FinalVersion = SVersion.ZeroVersion;
                FinalInformationalVersion = InformationalVersion.ZeroInformationalVersion;
            }
            else
            {
                Debug.Assert( CommitSha != null && finalVersion.IsValid );
                FinalVersion = finalVersion;
                FinalInformationalVersion = finalVersion.GetInformationalVersion( CommitSha, CommitDateUtc );
            }
        }

        /// <summary>
        /// Logs messages that express the state of this <see cref="RepositoryInfo"/>.
        /// </summary>
        /// <param name="logger">A target logger.</param>
        public void Explain( ILogger logger )
        {
            if( logger == null ) throw new ArgumentNullException( nameof( logger ) );
            if( Error != null )
            {
                logger.Error( Error );
                if( PossibleVersions != null ) LogPossibleVersions( logger );
            }
            else
            {
                Debug.Assert( CommitInfo != null );
                if( IsDirty )
                {
                    Debug.Assert( IsDirtyExplanations != null );
                    logger.Warn( "Working folder is Dirty! Checking this has been disabled since IgnoreDirtyWorkingFolder option is true." );
                    logger.Warn( IsDirtyExplanations );
                }
                var basic = CommitInfo.BasicInfo;
                if( basic == null )
                {
                    logger.Info( "No version information found on or below this commit." );
                }
                else
                {
                    logger.Info( ValidReleaseTag != null ? $"Tag: {ValidReleaseTag}" : "No tag found on the commit itself." );
                    logger.Info( BestCommitBelow != null ? $"Base tag below: {BestCommitBelow}" : "No base tag found below this commit." );
                    if( BetterExistingVersion != null )
                    {
                        logger.Warn( $"This commit has already been released with version '{BetterExistingVersion.ThisTag}', by commit '{BetterExistingVersion.CommitSha}'." );
                    }
                }
                if( CIRelease != null )
                {
                    logger.Info( $"CI release: '{FinalVersion.NormalizedText}' ({(CIRelease.IsZeroTimed ? nameof( CIBranchVersionMode.ZeroTimed ) : nameof( CIBranchVersionMode.LastReleaseBased ))})." );
                    LogPossibleVersions( logger );
                }
                else
                {
                    Debug.Assert( ValidReleaseTag != null, "Otherwise there is an Error." );
                    logger.Info( $"Release: '{FinalVersion.NormalizedText}'." );
                }
            }

            void LogPossibleVersions( ILogger logger )
            {
                Debug.Assert( PossibleVersions != null );
                string? opt = null;
                if( Options.OnlyPatch ) opt += "OnlyPatch";
                if( Options.SingleMajor.HasValue )
                {
                    if( opt != null ) opt += ", ";
                    opt += "SingleMajor = " + Options.SingleMajor.ToString();
                }
                if( opt != null ) opt = " (" + opt + ")";
                if( PossibleVersions.Count == 0 )
                {
                    logger.Info( $"No possible versions {opt}." );
                }
                else
                {
                    logger.Info( $"Possible version(s) {opt}: {string.Join( ", ", PossibleVersions )}" );
                }
            }
        }

        class ModifiedFile : IWorkingFolderModifiedFile
        {
            readonly Repository _r;
            readonly Commit _commit;
            readonly StatusEntry _entry;
            Blob? _committedBlob;
            string? _committedText;

            public ModifiedFile( Repository r, Commit commit, StatusEntry e, string entryFilePath )
            {
                Debug.Assert( entryFilePath == e.FilePath );
                _r = r;
                _commit = commit;
                _entry = e;
                Path = entryFilePath;
            }

            Blob GetBlob()
            {
                if( _committedBlob == null )
                {
                    TreeEntry e = _commit[Path];
                    Debug.Assert( e.TargetType == TreeEntryTargetType.Blob );
                    _committedBlob = (Blob)e.Target;
                }
                return _committedBlob;
            }

            public long CommittedContentSize => GetBlob().Size; 

            public Stream GetCommittedContent() => GetBlob().GetContentStream();

            public string CommittedText
            {
                get
                {
                    if( _committedText == null )
                    {
                        using( var s = GetCommittedContent() )
                        using( var r = new StreamReader( s ) )
                        {
                            _committedText = r.ReadToEnd();
                        }
                    }
                    return _committedText;
                }
            }

            public string Path { get; }

            public string FullPath => _r.Info.WorkingDirectory + Path; 

            public string RepositoryFullPath => _r.Info.WorkingDirectory;

        }

        string? ComputeIsDirty( Repository r, Commit commit, RepositoryInfoOptions options )
        {
            RepositoryStatus repositoryStatus = r.RetrieveStatus();
            int addedCount = repositoryStatus.Added.Count();
            int missingCount = repositoryStatus.Missing.Count();
            int removedCount = repositoryStatus.Removed.Count();
            int stagedCount = repositoryStatus.Staged.Count();
            StringBuilder? b = null;
            if( addedCount > 0 || missingCount > 0 || removedCount > 0 || stagedCount > 0 )
            {
                b = new StringBuilder( "Found: " );
                if( addedCount > 0 ) b.AppendFormat( "{0} file(s) added", addedCount );
                if( missingCount > 0 ) b.AppendFormat( "{0}{1} file(s) missing", b.Length > 10 ? ", " : null, missingCount );
                if( removedCount > 0 ) b.AppendFormat( "{0}{1} file(s) removed", b.Length > 10 ? ", " : null, removedCount );
                if( stagedCount > 0 ) b.AppendFormat( "{0}{1} file(s) staged", b.Length > 10 ? ", " : null, removedCount );
            }
            else
            {
                int fileCount = 0;
                foreach( StatusEntry m in repositoryStatus.Modified )
                {
                    string path = m.FilePath;
                    if( !options.IgnoreModifiedFiles.Contains( path )
                        && (options.IgnoreModifiedFilePredicate == null
                            || !options.IgnoreModifiedFilePredicate( new ModifiedFile( r, commit, m, path ) )) )
                    {
                        ++fileCount;
                        if( !options.IgnoreModifiedFileFullProcess )
                        {
                            Debug.Assert( b == null );
                            b = new StringBuilder( "At least one Modified file found: " );
                            b.Append( path );
                            break;
                        }
                        if( b == null )
                        {
                            b = new StringBuilder( "Modified file(s) found: " );
                            b.Append( path );
                        }
                        else if( fileCount <= 10 ) b.Append( ", " ).Append( path );
                    }
                }
                if( fileCount > 10 ) b!.AppendFormat( ", and {0} other file(s)", fileCount - 10 );
            }
            if( b == null ) return null;
            b.Append( '.' );
            return b.ToString();
        }

        /// <summary>
        /// Creates a new <see cref="RepositoryInfo"/> based on a path (that can be below the folder with the '.git' sub folder). 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="options">Optional <see cref="RepositoryInfoOptions"/>.</param>
        /// <returns>An immutable RepositoryInfo instance. Never null.</returns>
        public static RepositoryInfo LoadFromPath( string path, RepositoryInfoOptions? options = null )
        {
            if( path == null ) throw new ArgumentNullException( nameof( path ) );
            path = Repository.Discover( path );
            using( var repo = path != null ? new Repository( path ) : null )
            {
                return new RepositoryInfo( repo, options );
            }
        }

        /// <summary>
        /// Creates a new <see cref="RepositoryInfo"/> based on a path (that can be below the folder with the '.git' sub folder)
        /// and a function that can create a <see cref="RepositoryInfoOptions"/> from the actual Git repository path. 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="optionsBuilder">Function that can create a <see cref="RepositoryInfoOptions"/> from the Git working directory (the Solution folder).</param>
        /// <returns>An immutable RepositoryInfo instance. Never null.</returns>
        public static RepositoryInfo LoadFromPath( string path, Func<string,RepositoryInfoOptions> optionsBuilder )
        {
            if( path == null ) throw new ArgumentNullException( nameof( path ) );
            if( optionsBuilder == null ) throw new ArgumentNullException( nameof( optionsBuilder ) );

            path = Repository.Discover( path );
            using( var repo = path != null ? new Repository( path ) : null )
            {
                if( repo == null ) return new RepositoryInfo( null, null );
                return new RepositoryInfo( repo, optionsBuilder( repo.Info.WorkingDirectory ) );
            }
        }


        /// <summary>
        /// Creates a new <see cref="RepositoryInfo"/> based on a path (that can be below the folder with the '.git' sub folder). 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="logger">Logger that will be used.</param>
        /// <param name="optionsChecker">
        /// Optional action that accepts the logger, a boolean that is true if a RepositoryInfo.xml has been 
        /// found, and the <see cref="RepositoryInfoOptions"/> that will be used.
        /// </param>
        /// <returns>A RepositoryInfo instance.</returns>
        static public RepositoryInfo LoadFromPath( ILogger logger, string path, Action<ILogger, bool, RepositoryInfoOptions>? optionsChecker = null )
        {
            if( logger == null ) throw new ArgumentNullException( nameof( logger ) );
            return LoadFromPath( path, gitPath =>
            {
                string optionFile = Path.Combine( gitPath, "RepositoryInfo.xml" );
                bool fileExists = File.Exists( optionFile );
                var options = fileExists ? RepositoryInfoOptions.Read( optionFile ) : new RepositoryInfoOptions();
                if( optionsChecker == null )
                {
                    if( !fileExists ) logger.Info( "File RepositoryInfo.xml not found: using default options to read repository information." );
                    else logger.Info( "Using RepositoryInfo.xml: " + options.ToXml().ToString() );
                }
                else
                {
                    logger.Info( fileExists ? "RepositoryInfo.xml loaded." : "File RepositoryInfo.xml not found. Creating default RepositoryInfoOptions object." );
                    optionsChecker.Invoke( logger, fileExists, options );
                }
                return options;
            } );
        }
    }
}
