using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using LibGit2Sharp;

namespace SimpleGitVersion
{

    public partial class RepositoryInfo
    {
        /// <summary>
        /// Captures the first analysis of a repository based on a <see cref="RepositoryInfoOptions"/>.
        /// </summary>
        public readonly struct StartingCommitInfo
        {
            /// <summary>
            /// Gets the options from which this starting commit information is derived.
            /// </summary>
            public readonly RepositoryInfoOptions Options;

            /// <summary>
            /// Gets the fatal error text if locating this starting commit failed: when not null, it is one line of
            /// text like 'No Git repository.' or 'Unitialized Git repository.'.
            /// </summary>
            public readonly string? Error;

            /// <summary>
            /// Error code of the <see cref="Error"/>.
            /// </summary>
            public readonly RepositoryInfo.ErrorCodeStatus ErrorCode;

            /// <summary>
            /// Gets the commit. Null if it has not been resolved.
            /// </summary>
            public readonly Commit? Commit;

            /// <summary>
            /// Gets the name of the branches that have been considered.
            /// </summary>
            public readonly IReadOnlyCollection<string> ConsideredBranchNames;

            /// <summary>
            /// Gets the found branch option among the <see cref="RepositoryInfoOptions.Branches"/> based
            /// on the <see cref="ConsideredBranchNames"/> that must be used.
            /// </summary>
            public readonly RepositoryInfoOptionsBranch? FoundBranchOption;

            /// <summary>
            /// Gets the mode to use for CI build: this comes from the <see cref="FoundBranchOption"/>.
            /// </summary>
            public CIBranchVersionMode CIVersionMode => FoundBranchOption?.CIVersionMode ?? CIBranchVersionMode.None;

            /// <summary>
            /// Gets the branch name to use in final version name for CI build: this comes from the <see cref="FoundBranchOption"/>
            /// (this is the <see cref="RepositoryInfoOptionsBranch.VersionName"/> or the <see cref="RepositoryInfoOptionsBranch.Name"/>).
            /// Note that this name's length has not been checked.
            /// </summary>
            public string? CIBranchVersionName => FoundBranchOption == null
                                                    ? null
                                                    : String.IsNullOrWhiteSpace( FoundBranchOption.VersionName )
                                                        ? FoundBranchOption.Name
                                                        : FoundBranchOption.VersionName;

            /// <summary>
            /// Initializes a new <see cref="StartingCommitInfo"/>.
            /// </summary>
            /// <param name="options">The options to use.</param>
            /// <param name="r">The LibGit2Sharp's repository.</param>
            public StartingCommitInfo( RepositoryInfoOptions options, Repository? r )
            {
                Options = options;
                FoundBranchOption = null;
                ConsideredBranchNames = Array.Empty<string>();
                Commit = null;
                if( options.XmlMigrationRequired )
                {
                    ErrorCode = ErrorCodeStatus.OptionsXmlMigrationRequired;
                    Error = "Repository.xml format has changed. No more namespace and new SimpleGitVersion child element so that other components can easily use this central configuration file." + Environment.NewLine
                            + "It should be:" + Environment.NewLine
                            + new XDocument( new XElement( XNamespace.None + "RepositoryInfo", options.ToXml() ) ).ToString();
                    return;
                }
                if( r == null )
                {
                    ErrorCode = ErrorCodeStatus.InitNoGitRepository;
                    Error = "No Git repository.";
                    return;
                }
                string? objectish = options.HeadCommit;

                IReadOnlyCollection<string>? branchNames = null;
                // Find current commit (the head) if none is provided.
                if( String.IsNullOrWhiteSpace( objectish ) )
                {
                    if( String.IsNullOrWhiteSpace( options.HeadBranchName ) )
                    {
                        // locCommit is here because one cannot use an out parameter inside a lambda.
                        Commit = r.Head.Tip;
                        if( Commit == null )
                        {
                            ErrorCode = ErrorCodeStatus.InitUnitializedGitRepository;
                            Error = "Unitialized Git repository.";
                            return;
                        }
                        // Save the branches!
                        // By doing this, when we are in 'Detached Head' state (the head of the repository is on a commit and not on a branch: git checkout <sha>),
                        // we can detect that it is the head of a branch and hence apply possible options (mainly CI) for it.
                        // We take into account the local branches and only the branches from options.RemoteName remote here.
                        string branchName = r.Head.FriendlyName;
                        if( branchName == "(no branch)" )
                        {
                            branchNames = FindBranches( Commit, options, r );
                        }
                        else
                        {
                            branchNames = new[] { branchName };
                        }
                    }
                    else
                    {
                        Debug.Assert( options.HeadBranchName != null, "A HeadBranchName has been specified." );
                        string remotePrefix = options.RemoteName + '/';
                        string localBranchName = options.HeadBranchName.StartsWith( remotePrefix )
                                                    ? options.HeadBranchName.Substring( remotePrefix.Length )
                                                    : options.HeadBranchName;
                        Branch br = r.Branches[options.HeadBranchName];
                        if( br == null && ReferenceEquals( localBranchName, options.HeadBranchName ) )
                        {
                            string remoteName = remotePrefix + options.HeadBranchName;
                            br = r.Branches[remoteName];
                            if( br == null )
                            {
                                ErrorCode = ErrorCodeStatus.InitHeadBranchNameNotFound;
                                Error = $"Unknown HeadBranchName: '{options.HeadBranchName}' (also tested on remote '{remoteName}').";
                                return;
                            }
                        }
                        if( br == null )
                        {
                            ErrorCode = ErrorCodeStatus.InitHeadRemoteBranchNameNotFound;
                            Error = $"Unknown (remote) HeadBranchName: '{options.HeadBranchName}'.";
                            return;
                        }
                        Commit = br.Tip;
                        branchNames = new[] { localBranchName };
                    }
                    ErrorCode = ErrorCodeStatus.None;
                    Error = null;
                }
                else
                {
                    Commit = r.Lookup( objectish )?.Peel<Commit>();
                    if( Commit == null )
                    {
                        ErrorCode = ErrorCodeStatus.InitHeadCommitNotFound;
                        Error = $"Unable to find HeadCommit '{objectish}' commit.";
                    }
                    else
                    {
                        ErrorCode = ErrorCodeStatus.None;
                        Error = null;
                        branchNames = FindBranches( Commit, options, r );
                    }
                }
                if( branchNames != null )
                {
                    ConsideredBranchNames = branchNames;
                    if( options.Branches != null )
                    {
                        FoundBranchOption = options.Branches.FirstOrDefault( b => branchNames.Contains( b.Name ) );
                    }
                }

                static IReadOnlyCollection<string> FindBranches( Commit commit, RepositoryInfoOptions options, Repository r )
                {
                    string remotePrefix = options.RemoteName + '/';
                    return r.Branches
                            .Where( b => b.Tip == commit && (!b.IsRemote || b.FriendlyName.StartsWith( remotePrefix )) )
                            .Select( b => b.IsRemote ? b.FriendlyName.Substring( remotePrefix.Length ) : b.FriendlyName )
                            .ToArray();
                }
            }
        }

    }
}
