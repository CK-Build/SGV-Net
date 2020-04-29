using System;
using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace SimpleGitVersion
{

    public partial class RepositoryInfo
    {
        /// <summary>
        /// Captures the first analysis of a repository based on a <see cref="RepositoryInfoOptions"/>.
        /// </summary>
        public readonly struct StartingCommit
        {
            /// <summary>
            /// Gets the options from which this starting commit information is derived.
            /// </summary>
            public readonly RepositoryInfoOptions Options;

            /// <summary>
            /// Gets the fatal error text if locating this starting commit failed: when not null, it is one line of text like 'No Git repository.' or 'Unitialized Git repository.'.
            /// </summary>
            public readonly string? Error;

            /// <summary>
            /// Gets the commit. Null if it has not been resolved.
            /// </summary>
            public readonly Commit? Commit;

            /// <summary>
            /// Gets the name of the repository branches that have been considered.
            /// This is null if <see cref="RepositoryInfoOptions.StartingCommitSha"/> has been specified (or if a
            /// repository error occured).
            /// </summary>
            public readonly IReadOnlyCollection<string>? ConsideredBranchNames;

            /// <summary>
            /// Gets the mode to use for CI build: this comes from the <see cref="RepositoryInfoOptions.Branches"/>
            /// and the <see cref="ConsideredBranchNames"/>.
            /// </summary>
            public readonly CIBranchVersionMode CIVersionMode;

            /// <summary>
            /// Gets the branch name to use in final version name for CI build: this comes from the <see cref="RepositoryInfoOptions.Branches"/>
            /// (this is the <see cref="RepositoryInfoOptionsBranch.VersionName"/> or the <see cref="RepositoryInfoOptionsBranch.Name"/>) and
            /// the <see cref="ConsideredBranchNames"/>.
            /// This is null if a CI enabled branch has not been found in the options.
            /// Note that this name's length has not been checked.
            /// </summary>
            public readonly string? CIBranchVersionName;

            /// <summary>
            /// Initializes a new <see cref="StartingCommit"/>.
            /// </summary>
            /// <param name="options">The options to use.</param>
            /// <param name="r">The LibGit2Sharp's repository.</param>
            public StartingCommit( RepositoryInfoOptions options, Repository? r )
            {
                Options = options;
                CIVersionMode = CIBranchVersionMode.None;
                ConsideredBranchNames = null;
                Commit = null;
                CIBranchVersionName = null;
                if( r == null )
                {
                    Error = "No Git repository.";
                    return;
                }
                string? commitSha = options.StartingCommitSha;

                // Find current commit (the head) if none is provided.
                if( String.IsNullOrWhiteSpace( commitSha ) )
                {
                    IReadOnlyCollection<string> branchNames;
                    if( String.IsNullOrWhiteSpace( options.StartingBranchName ) )
                    {
                        // locCommit is here because one cannot use an out parameter inside a lambda.
                        var locCommit = Commit = r.Head.Tip;
                        if( locCommit == null )
                        {
                            Error = "Unitialized Git repository.";
                            return;
                        }
                        // Save the branches!
                        // By doing this, when we are in 'Detached Head' state (the head of the repository is on a commit and not on a branch: git checkout <sha>),
                        // we can detect that it is the head of a branch and hence apply possible options (mainly CI) for it.
                        // We take into account only the branches from options.RemoteName remote here.
                        string branchName = r.Head.FriendlyName;
                        if( branchName == "(no branch)" )
                        {
                            string remotePrefix = options.RemoteName + '/';
                            branchNames = r.Branches
                                           .Where( b => b.Tip == locCommit && (!b.IsRemote || b.FriendlyName.StartsWith( remotePrefix )) )
                                           .Select( b => b.IsRemote ? b.FriendlyName.Substring( remotePrefix.Length ) : b.FriendlyName )
                                           .ToArray();
                        }
                        else
                        {
                            branchNames = new[] { branchName };
                        }
                    }
                    else
                    {
                        string remotePrefix = options.RemoteName + '/';
                        string localBranchName = options.StartingBranchName!.StartsWith( remotePrefix )
                                                    ? options.StartingBranchName.Substring( remotePrefix.Length )
                                                    : options.StartingBranchName;
                        Branch br = r.Branches[options.StartingBranchName];
                        if( br == null && ReferenceEquals( localBranchName, options.StartingBranchName ) )
                        {
                            string remoteName = remotePrefix + options.StartingBranchName;
                            br = r.Branches[remoteName];
                            if( br == null )
                            {
                                Error = $"Unknown StartingBranchName: '{options.StartingBranchName}' (also tested on remote '{remoteName}').";
                                return;
                            }
                        }
                        if( br == null )
                        {
                            Error = $"Unknown (remote) StartingBranchName: '{options.StartingBranchName}'.";
                            return;
                        }
                        Commit = br.Tip;
                        branchNames = new[] { localBranchName };
                    }
                    ConsideredBranchNames = branchNames;
                    RepositoryInfoOptionsBranch bOpt;
                    if( options.Branches != null
                        && (bOpt = options.Branches.FirstOrDefault( b => branchNames.Contains( b.Name ) )) != null
                        && bOpt.CIVersionMode != CIBranchVersionMode.None )
                    {
                        CIVersionMode = bOpt.CIVersionMode;
                        CIBranchVersionName = String.IsNullOrWhiteSpace( bOpt.VersionName ) ? bOpt.Name : bOpt.VersionName;
                    }
                    Error = null;
                }
                else
                {
                    Commit = r.Lookup<Commit>( commitSha );
                    if( Commit == null )
                    {
                        Error = $"Commit '{commitSha}' not found.";
                    }
                    else
                    {
                        Error = null;
                    }
                }
            }
        }

    }
}
