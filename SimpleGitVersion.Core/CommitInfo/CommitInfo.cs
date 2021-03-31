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
    public partial class CommitInfo : ICommitInfo
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
        /// Its <see cref="InitialInfo.Error"/> holds the primary error that may prevent any further analysis.
        /// </summary>
        public readonly InitialInfo StartingCommit;

        /// <summary>
        /// Gets either the <see cref="StartingCommit"/>'s error or any error that occurred during
        /// the subsequent analysis.
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets the <see cref="ErrorCodeStatus"/> for the <see cref="Error"/>.
        /// </summary>
        public ErrorCodeStatus ErrorCode { get; }

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
        /// If the <see cref="RepositoryInfoOptions.CheckExistingVersions"/> was true then:
        /// <list type="bullet">
        ///   <item>If there is a <see cref="RepositoryInfoOptions.StartingVersion"/>, the first version must be this starting one.</item>
        ///   <item>If there is no <see cref="RepositoryInfoOptions.StartingVersion"/>, the first version must be one of the <see cref="CSVersion.FirstPossibleVersions"/>.</item>
        ///   <item>Existing version tags must always be compact (ie. no "holes" must exist between them).</item>
        /// </list>
        /// </para>
        /// </summary>
        public readonly IReadOnlyList<ITagCommit>? ExistingVersions;

        /// <inheritdoc/>
        public ITagCommit? AlreadyExistingVersion { get; }

        /// <inheritdoc/>
        public ITagCommit? BestCommitBelow { get; }

        /// <inheritdoc/>
        public CSVersion? ReleaseTag { get; }

        /// <summary>
        /// Gets the <see cref="SimpleGitVersion.DetailedCommitInfo"/> of the current commit point.
        /// Null if there is a <see cref="Error"/> that prevented its computation.
        /// </summary>
        public readonly DetailedCommitInfo? DetailedCommitInfo;

        /// <summary>
        /// Gets whether the parent graph of the <see cref="DetailedCommitInfo.BasicInfo"/> has not been fully anlayzed
        /// because we are on a shallow cloned repositry.
        /// </summary>
        public bool IsShallowCloned { get; }

        /// <summary>
        /// When empty, this means that there cannot be a valid release tag on the current commit point.
        /// Null when an <see cref="Error"/> prevented its computation.
        /// This is the set of filtered versions (<see cref="RepositoryInfoOptions.SingleMajor"/>
        /// and <see cref="RepositoryInfoOptions.OnlyPatch"/> options are applied
        /// on <see cref="DetailedCommitInfo"/>.<see cref="DetailedCommitInfo.PossibleVersions">PossibleVersions</see>).
        /// </summary>
        public IReadOnlyList<CSVersion>? PossibleVersions { get; }

        /// <summary>
        /// These are the versions that may be available to any commit above the current one.
        /// Null when a <see cref="StartingCommit"/>'s error or a subsequent analysis error prevented its computation.
        /// This is the set of filtered versions (<see cref="RepositoryInfoOptions.SingleMajor"/>
        /// and <see cref="RepositoryInfoOptions.OnlyPatch"/> options are applied
        /// on <see cref="DetailedCommitInfo"/>.<see cref="DetailedCommitInfo.NextPossibleVersions">NextPossibleVersions</see>).
        /// Empty if there is a <see cref="Error"/> that prevented its computation.
        /// </summary>
        public IReadOnlyList<CSVersion>? NextPossibleVersions { get; }

        /// <summary>
        /// Gets CI informations if a CI release can be done: <see cref="ReleaseTag"/> is necessarily null.
        /// Not null only if we are on a branch that is enabled in <see cref="RepositoryInfoOptions.Branches"/> (either 
        /// because it is the current branch or <see cref="RepositoryInfoOptions.HeadBranchName"/> specifies it),
        /// and the <see cref="RepositoryInfoOptions.HeadCommit"/> is null or empty.
        /// </summary>
        public ICIReleaseInfo? CIRelease { get; }

        /// <summary>
        /// Gets the final version (short form if it is a <see cref="CSVersion"/>) that must be used to build this commit point.
        /// Never null: defaults to <see cref="SVersion.ZeroVersion"/>.
        /// </summary>
        public readonly SVersion FinalVersion;

        /// <summary>
        /// Gets the standardized information version string that must be used to build this commit point.
        /// If an error prevented the <see cref="FinalVersion"/> computation but a <see cref="CommitSha"/> has been
        /// found, this is based on the <see cref="SVersion.ZeroVersion"/> with this CommitSha and <see cref="CommitDateUtc"/>.
        /// Never null: ultimately defaults to <see cref="InformationalVersion.ZeroInformationalVersion"/> string.
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
        /// Initializes a new <see cref="CommitInfo"/> on a LibGit2Sharp <see cref="Repository"/>.
        /// </summary>
        /// <param name="r">The repository (can be invalid and even null).</param>
        /// <param name="options">Optional options.</param>
        public CommitInfo( Repository? r, RepositoryInfoOptions? options = null )
        {
            CommitDateUtc = InformationalVersion.ZeroCommitDate;
            SVersion? finalVersion = null;
            StartingCommit = new InitialInfo( options ??= new RepositoryInfoOptions(), r );
            if( StartingCommit.Error != null )
            {
                Debug.Assert( StartingCommit.ErrorCode != ErrorCodeStatus.None );
                ErrorCode = StartingCommit.ErrorCode;
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
                    ErrorCode = ErrorCodeStatus.DirtyWorkingFolder;
                    errors.AppendLine( "Working folder has non committed changes." );
                    errors.Append( IsDirtyExplanations );
                }
                else 
                {
                    TagCollector collector = new TagCollector( errors,
                                                               r,
                                                               options.StartingVersion,
                                                               options.OverriddenTags,
                                                               options.SingleMajor,
                                                               options.CheckExistingVersions );
                    if( errors.Length > 0 )
                    {
                        Debug.Assert( collector.ErrorCode != ErrorCodeStatus.None );
                        ErrorCode = collector.ErrorCode;
                    }
                    else
                    {
                        Debug.Assert( collector.ErrorCode == ErrorCodeStatus.None );
                        Debug.Assert( collector.ExistingVersions != null );

                        ExistingVersions = collector.ExistingVersions.TagCommits;
                        DetailedCommitInfo info = collector.GetCommitInfo( commit );
                        DetailedCommitInfo = info;
                        IsShallowCloned = info.IsShallowCloned;
                        AlreadyExistingVersion = info.AlreadyExistingVersion;
                        BestCommitBelow = info.BestCommitBelow;

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

                        ITagCommit? thisReleaseTag = info.BasicInfo?.UnfilteredThisCommit;

                        if( thisReleaseTag != null )
                        {
                            ReleaseTag = thisReleaseTag.ThisTag;

                            if( PossibleVersions.Contains( ReleaseTag ) )
                            {
                                finalVersion = ReleaseTag;
                            }
                            else
                            {
                                errors.Append( "Release tag '" )
                                        .Append( ReleaseTag )
                                        .AppendLine( "' is not valid here. " );
                                if( PossibleVersions != rawPossible && rawPossible.Contains( ReleaseTag ) )
                                {
                                    errors.AppendLine( "Note: this version is invalid because of <SingleMajor> or <OnlyPatch> setting in RepositoryInfo.xml." );
                                    ErrorCode = ErrorCodeStatus.ReleaseTagConflictsWithSingleMajorOrOnlyPatch;
                                }
                                else
                                {
                                    ErrorCode = ErrorCodeStatus.ReleaseTagIsNotPossible;
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
                                finalVersion = CIRelease.BuildVersion;
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
                                    ErrorCode = ErrorCodeStatus.CIBuildVersionNameTooLong;
                                }
                                else if( StartingCommit.ConsideredBranchNames.Count == 0 )
                                {
                                    Debug.Assert( options.HeadBranchName == null, "If the HeadBranchName has not been found, it's in StartingCommit.Error." );
                                    if( !String.IsNullOrEmpty( options.HeadCommit ) )
                                    {
                                        errors.AppendLine( $"no branches reference the specified commit '{options.HeadCommit}'." );
                                        ErrorCode = ErrorCodeStatus.CIBuildHeadCommitIsDetached;
                                    }
                                    else 
                                    {
                                        errors.AppendLine( "no branches reference the current repository's head commit." );
                                        ErrorCode = ErrorCodeStatus.CIBuildRepositoryHeadIsDetached;
                                    }
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
                                            .AppendLine( @"  <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""explo"" UseReleaseBuildConfigurationFrom=""CI"" />." )
                                            .AppendLine( @"  <Branch Name=""fx/new-computation"" CIVersionMode=""ZeroTimed"" VersionName=""explo"" />." )
                                            .AppendLine( "</Branches>" );
                                    ErrorCode = ErrorCodeStatus.CIBuildMissingBranchOption;
                                }
                                else
                                {
                                    errors.AppendLine( $@"configured CI branch '{StartingCommit.FoundBranchOption.Name}' found explicitly states that CIVersionMode=""None""." )
                                            .AppendLine( @"Use CIVersionMode=""ZeroTimed"" or CIVersionMode=""LastReleaseBased"" to enable CI build on this branch." );
                                    ErrorCode = ErrorCodeStatus.CIBuildBranchExplicitNone;
                                }
                            }
                        }
                    }
                }

                if( errors.Length > 0 )
                {
                    Error = errors.ToString();
                    Debug.Assert( ErrorCode != ErrorCodeStatus.None );
                }
                Debug.Assert( (Error != null) != (finalVersion != null), "If there is an error, then there is a no final version. And vice versa." );

                // If there is a AlreadyExistingVersion and it is not ignored:
                // - There cannot be any PossibleVersions for this commit point (but this is not an error and this must be specified even if we are on error).
                // - If a final version were to be produced, it is canceled (and an error is created)
                //      EXCEPT if we are "saving the 0000 LastReleaseBased version".
                //          - It is a CI Release with a Depth of 0 (below or unrelated to the commit that has the version).
                //          - The existing version has been using a different build configuration than this one (typically this allows a "Debug" set 
                //            of artifacts to be produced even if a "Release" exists). 
                if( !options.IgnoreAlreadyExistingVersion
                    && AlreadyExistingVersion != null )
                {
                    PossibleVersions = CSVersion.EmptyArray;
                    if( finalVersion != null
                        && (CIRelease == null
                            || CIRelease.IsZeroTimed
                            || CIRelease.Depth > 0
                            || BuildConfigurationSelector( StartingCommit, CIRelease.BaseTag ) == BuildConfigurationSelector( StartingCommit, finalVersion ) ) )
                    {
                        errors.AppendLine( AlreadyExistingVersionMessage( AlreadyExistingVersion ) )
                                .AppendLine( "To ignore such already existing version and force a release, set IgnoreAlreadyExistingVersion option to true." );
                        // There is a valid ReleaseTag or a CIRelease but no more final version.
                        finalVersion = null;
                        Error = errors.ToString();
                        ErrorCode = ErrorCodeStatus.AlreadyExistingVersion;
                    }
                }
            }

            Debug.Assert( (Error != null) == (ErrorCode != ErrorCodeStatus.None), "If there is an error message, then there is an error code. And vice versa." );
            Debug.Assert( (Error != null) != (finalVersion != null), "If there is an error, then there is a no final version. And vice versa." );

            FinalVersion = finalVersion ?? SVersion.ZeroVersion;
            FinalInformationalVersion = CommitSha != null
                                           ? FinalVersion.GetInformationalVersion( CommitSha, CommitDateUtc )
                                           : InformationalVersion.ZeroInformationalVersion;
        }

        /// <summary>
        /// Logs messages that express the state of this <see cref="CommitInfo"/>.
        /// </summary>
        /// <param name="logger">A target logger.</param>
        public void Explain( ILogger logger )
        {
            if( logger == null ) throw new ArgumentNullException( nameof( logger ) );
            if( Error != null )
            {
                logger.Error( Error );
                if( DetailedCommitInfo != null ) LogCommitInfo( logger );
                if( PossibleVersions != null ) LogPossibleVersions( logger );
            }
            else
            {
                Debug.Assert( DetailedCommitInfo != null );
                Debug.Assert( FinalVersion != null && FinalVersion != SVersion.ZeroVersion );
                if( IsDirty )
                {
                    Debug.Assert( IsDirtyExplanations != null );
                    logger.Warn( "Working folder is Dirty! Checking this has been disabled since IgnoreDirtyWorkingFolder option is true." );
                    logger.Warn( IsDirtyExplanations );
                }
                LogCommitInfo( logger );
                if( CIRelease != null )
                {
                    LogPossibleVersions( logger );
                    logger.Info( $"CI release: '{FinalVersion}' ({(CIRelease.IsZeroTimed ? nameof( CIBranchVersionMode.ZeroTimed ) : nameof( CIBranchVersionMode.LastReleaseBased ))})." );
                }
                else
                {
                    Debug.Assert( ReleaseTag != null, "Otherwise there is an Error." );
                    logger.Info( $"Release: '{ReleaseTag}'." );
                }
            }

            void LogCommitInfo( ILogger logger )
            {
                var basic = DetailedCommitInfo.BasicInfo;
                if( basic == null )
                {
                    logger.Info( "No version information found on or below this commit." );
                }
                else
                {
                    logger.Info( ReleaseTag != null ? $"Tag: {ReleaseTag}" : "No tag found on the commit itself." );
                    logger.Info( BestCommitBelow != null ? $"Base tag below: {BestCommitBelow}" : "No base tag found below this commit." );
                    if( AlreadyExistingVersion != null && Error == null )
                    {
                        logger.Warn( AlreadyExistingVersionMessage( AlreadyExistingVersion ) );
                    }
                    if( basic.IsShallowCloned )
                    {
                        logger.Warn( "The parent graph analysis is not complete because we are on a shallow cloned repositry." );
                    }
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
                    logger.Warn( $"No possible versions {opt}." );
                }
                else
                {
                    logger.Info( $"Possible version(s) {opt}: {string.Join( ", ", PossibleVersions )}" );
                }
            }

        }


        static string AlreadyExistingVersionMessage( ITagCommit betterTag )
        {
            return $"This commit has already been released with version '{betterTag.ThisTag}', by commit '{betterTag.CommitSha}'.";
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
        /// Creates a new <see cref="CommitInfo"/> based on a path (that can be below the folder with the '.git' sub folder). 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="options">Optional <see cref="RepositoryInfoOptions"/>.</param>
        /// <returns>An immutable RepositoryInfo instance. Never null.</returns>
        public static CommitInfo LoadFromPath( string path, RepositoryInfoOptions? options = null )
        {
            if( path == null ) throw new ArgumentNullException( nameof( path ) );
            path = Repository.Discover( path );
            using( var repo = path != null ? new Repository( path ) : null )
            {
                return new CommitInfo( repo, options );
            }
        }

        /// <summary>
        /// Creates a new <see cref="CommitInfo"/> based on a path (that can be below the folder with the '.git' sub folder)
        /// and a function that can create a <see cref="RepositoryInfoOptions"/> from the actual Git repository path. 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="optionsBuilder">Function that can create a <see cref="RepositoryInfoOptions"/> from the Git working directory (the Solution folder).</param>
        /// <returns>An immutable RepositoryInfo instance. Never null.</returns>
        public static CommitInfo LoadFromPath( string path, Func<string,RepositoryInfoOptions> optionsBuilder )
        {
            if( path == null ) throw new ArgumentNullException( nameof( path ) );
            if( optionsBuilder == null ) throw new ArgumentNullException( nameof( optionsBuilder ) );

            path = Repository.Discover( path );
            using( var repo = path != null ? new Repository( path ) : null )
            {
                if( repo == null ) return new CommitInfo( null, null );
                return new CommitInfo( repo, optionsBuilder( repo.Info.WorkingDirectory ) );
            }
        }


        /// <summary>
        /// Creates a new <see cref="CommitInfo"/> based on a path (that can be below the folder with the '.git' sub folder). 
        /// </summary>
        /// <param name="path">The path to lookup.</param>
        /// <param name="logger">Logger that will be used.</param>
        /// <param name="optionsChecker">
        /// Optional action that accepts the logger, a boolean that is true if a RepositoryInfo.xml has been 
        /// found, and the <see cref="RepositoryInfoOptions"/> that will be used.
        /// </param>
        /// <returns>A RepositoryInfo instance.</returns>
        static public CommitInfo LoadFromPath( ILogger logger, string path, Action<ILogger, bool, RepositoryInfoOptions>? optionsChecker = null )
        {
            if( logger == null ) throw new ArgumentNullException( nameof( logger ) );
            return LoadFromPath( path, gitPath =>
            {
                string optionFile = Path.Combine( gitPath, "RepositoryInfo.xml" );
                bool fileExists = File.Exists( optionFile );
                var options = fileExists ? RepositoryInfoOptions.Read( optionFile ) : new RepositoryInfoOptions();
                if( optionsChecker == null )
                {
                    logger.Info( fileExists
                                    ? "Using RepositoryInfo.xml, with SimpleGitVersion element: " + options.ToXml().ToString()
                                    : "File RepositoryInfo.xml not found. Creating default RepositoryInfoOptions object." );
                }
                else
                {
                    logger.Info( fileExists
                                    ? "RepositoryInfo.xml loaded."
                                    : "File RepositoryInfo.xml not found. Creating default RepositoryInfoOptions object." );
                    optionsChecker.Invoke( logger, fileExists, options );
                }
                return options;
            } );
        }
    }
}
