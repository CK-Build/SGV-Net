
# SGV-Net - SimpleGitVersion = CSemVer ♥ Git


CSemVer (http://csemver.org) is an operational subset of Semantic Versioning (http://semver.org) that provides stronger
and explicit definition of a version number.

SimpleGitVersion applies CSemVer to Git repositories. Tools can use it to ensure **ensuring traceability and deployment reliability** by:
 - Validating version tags on any commit (typically the HEAD) by analyzing the repository topology and applying CSemVer rules.
 - Computing automatic CSemVer-CI versions on CI branches.

## Configuration
SimpleGitVersion supports a few configurations (like defining which branches will produce CI-Build) thanks to
an optional **RepositoryInfo.xml** file that is described [here](SimpleGitVersion.Core).

## Build status & packages:

- Stable release [![Build status](https://ci.appveyor.com/api/projects/status/at6fx86w6qbkxclg/branch/master?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/sgv-net/branch/master)
  -  SimpleGitVersion.Abstractions ![Nuget](https://img.shields.io/nuget/v/SimpleGitVersion.Abstractions?logo=nuget)
  -  SimpleGitVersion.Core ![Nuget](https://img.shields.io/nuget/v/SimpleGitVersion.Core?logo=nuget)
  -  SimpleGitVersion.Cake ![Nuget](https://img.shields.io/nuget/v/SimpleGitVersion.Cake?logo=nuget)

- Development [![Build status](https://ci.appveyor.com/api/projects/status/at6fx86w6qbkxclg/branch/develop?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/sgv-net/branch/develop)
  - CI Builds packages can be found on the [public Azure feeed](https://dev.azure.com/Signature-OpenSource/Feeds/_packaging?_a=feed&feed=NetCore3).
 
The actual code is in SimpleGitVersion.Core package that depends on
 - [CSemVer](https://www.nuget.org/packages/CSemVer/) that implements SemVer and CSemVer versions (parsing and version manipulations).
 - [LibGit2Sharp](https://www.nuget.org/packages/LibGit2Sharp/) to read the local Git repositories.

Only one tool is currently implemented and supported:
  - [SimpleGitVersion.Cake](SimpleGitVersion.Cake) enables Cake build system (http://cakebuild.net/) to use CSemVer on Git repositories.

## How it works

### Git Basics
CSemVer (http://csemver.org) can be applied to Git repository: this is the goal of the SimpleGitVersion project.
To understand it, try to forget the Git branches and consider the basics of Git:
  - A commit point has a ‘content’: its ‘File tree’ is our ‘base of code’.
  - A commit can have tags: in our case, when a commit is tagged with ‘v4.5.0-rc.2’ this means that the ‘File tree’ of this commit contains and defines everything we need to build this exact version of our product/artifact.
  - Tags are unique across the repository: there can be only one commit tagged with ‘v4.5.0-rc.2’.
  - More than one commits can contain identical File trees and this is easy to detect: these File trees share the same SHA1 that we call ContentSHA 
  (this is how Git works, by using a [Merkle Tree](https://en.wikipedia.org/wiki/Merkle_tree)).
  - A commit point is either:
    - An orphan (the initial commit in ‘master’ commit for instance)
    - Based on one commit: it carries the differences between itself and its Parent.
    - Based on 2 (or more) commits: it merges its 2 (or more) Parents’ File tree into one File tree.
  - There cannot be cycles in the graph. This is a classic DAG.

This is our playground for Git, no more no less. You may wonder where the branches are in this picture. We don’t use branches.
Of course, branches help, they are important to organize the repository (and the work), but we consider them to live at a higher
level of organization.

---
**Claim:**

Proper versioning can, and should, be achieved by considering only the commits and the topology of the repository.

---

One of the goal of SimpleGitVersion is to allow the release of a version N+ on a commit after a version N (N+ being greater than N)
if and only if the ‘base of code’ of N appears in the parents of the N+ commit. You cannot _forget_ commits, you need to include
them - and may be explicitly undo/revert their content if needed.


### The algorithm
SimpleGitVersion can compute two sets of versions for any commit `C` in the repository.
The first step is to compute the Base Version of `C`: 
  - Considering `P(C)`, all the parent commits of `C`.
  - Computes the Base Version `Bv` of `C` based on:
    - `Tc` = The greatest Version Tag that appears in `P(C)`.
    - `Cc` = The greatest Version Tag that appears on the commits' file tree (ie. considering the ContentSHA) in `P(C)`.
    - `0v` = The special no-version Tag (its successors are the Very First Possible Versions).
    - Base Version is: 
      - `Bv` = Max(`Tc`, `Cc`)
      - `Bv` = {`0v`} when there is no `Tc` nor `Cc`.

Based on this Base Version, two sets of versions are computed for `C`:
 - PossibleVersions: The versions that are valid for `C` regardless of any current Tag on `C` itself. 
 - NextPossibleVersions: The versions that may appear on any future commits based on `C`.

### The implementation (C#)

Go to code, the TagCollector and its friends do the job [here](SimpleGitVersion.Core/TagCollector).

## Final versions produced

The ultimate goal of versioning tool like this one is to produce versions.
The produced versions will appear in numerous place in different forms.

### AssemblyVersion
The assembly version consists only of the Major.Minor numbers: the third and fourth numbers are always 0.
This follows the semantic versioning rules and is "a common approach that gives you the ability to roll out hot fixes to
your assembly without breaking existing applications that may be referencing it"
(excerpt from [GitVersion documentation](https://gitversion.net/docs/more-info/variables)). 

### AssemblyInformationalVersion
The informational version is displayed in the file's property window (see captures below). 
SimpleGitVersion uses the implementation available in CSemVer.
https://github.com/CK-Build/CSemVer-Net/blob/develop/CSemVer/InformationalVersion.cs

It is this string: `$"{versionShort}/{commitSha}/{commitDateUtc.ToString( "u" )}"`

And the Zero Version is:

`"0.0.0-0/0000000000000000000000000000000000000000/0001-01-01 00:00:00Z"`

Note that is has no contextual information like user name or build machine name to enable reproducible builds.

### Windows File Version (AssemblyFileVersion)
Each CSemVer version is associated to a unique number between 1 (v0.0.0-a) and 4000050000000000000 (v99999.49999.9999).
This number requires 63 bits: a positive long (signed 64 bits integer) is enough.

The windows FILEVERSION is a binary version number for the file that is displayed in the property window.
The version consists of two 32-bit integers, defined by four 16-bit integers.
SimpleGitVersion uses the whole 64 bits for the file version: the CSemVer version is multiplied by 2
and when generating a CI-Build, 1 is added.

![File properties for CI-Build: file version is odd.](https://raw.githubusercontent.com/SimpleGitVersion/SimpleGitVersion.github.io/master/resources/WindowsFileProperties-CI.png)

For CI-Build, file version is odd. For a release, the file version is even:

![File properties for release: file version is even.](https://raw.githubusercontent.com/SimpleGitVersion/SimpleGitVersion.github.io/master/resources/WindowsFileProperties-alpha.png)

When the release is invalid, the file version is 0.0.0.0 (and the _Product Version_ may contain the error message).

![File properties for invalid release: file version is zero.](https://raw.githubusercontent.com/SimpleGitVersion/SimpleGitVersion.github.io/master/resources/WindowsFileProperties-Invalid.png)


## Background & links

Please have a look at http://www.lionhack.com/2014/03/09/software-versioning-strategies/: this is a very good overview of
the domain that explains .Net Version attributes, FileVersion attributes, version schemes, packages, etc. *
Of course, semantic versioning is a must: http://semver.org/ as well as http://csemver.org/.

To discover NuGet:
 - https://docs.microsoft.com/en-us/nuget/reference/package-versioning
 - http://www.xavierdecoster.com/semantic-versioning-auto-incremented-nuget-package-versions
 - NuGet - Top 10 NuGet (Anti-) Patterns: https://msdn.microsoft.com/en-us/magazine/jj851071.aspx 
 - http://haacked.com/archive/2011/10/24/semver-nuget-nightly-builds.aspx/ (even if it’s an old post, the discussion below the post is interesting)

About Git branches and workflows, read http://nvie.com/posts/a-successful-git-branching-model/.
You should also have a look at https://github.com/ParticularLabs/GitVersion/ that pursue the same goal as SimpleGitVersion but
differently (it does not use CSemVer and relies on your Git flow).

### Many thanks to our dependencies!

SimpleGitVersion.Core only depends on https://github.com/libgit2/libgit2sharp to interact with Git repository.

SGV-Net solution uses NUnit, CodeCake (https://github.com/SimpleGitVersion/CodeCake that is based on https://cakebuild.net - see [here](CodeCakeBuilder))
to manage its own version and build chain.

