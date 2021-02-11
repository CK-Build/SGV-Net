
# SimpleGitVersion.Core

Implements [SimpleGitVersion.Abstractions](../SimpleGitVersion.Abstractions) thanks to the excellent [LibGit2Sharp](https://www.nuget.org/packages/LibGit2Sharp).

The [CommitInfo](CommitInfo/CommitInfo.cs) is the main object of this library.
The hard work is done by the [TagCollector](TagCollector).

Configurations can be done thanks to the optional **RepositoryInfo.xml** file that may appear at the root level in the repository.

## RepositoryInfo.xml: the &lt;SimpleGitVersion&gt; element

This file can contain a &lt;SimpleGitVersion&gt;...&lt;/SimpleGitVersion&gt; element that define options like
CI builds or StartingVersionForCSemVer that drives the behavior of *SimpleGitVersion.Core*.

### StartingVersionForCSemVer

This defines the first version that should be considered in the repository:
```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <StartingVersionForCSemVer>v0.6.0-rc</StartingVersionForCSemVer>
  </SimpleGitVersion>
</RepositoryInfo>
```

When this version is specified, any existing commit tag with a version lower than it is simply ignored.
This is typically used to start using CSemVer on a repository that did not use it before and therefore
has incoherent or invalid tags.

Another common use is when for any reason one need to boost the actual version: jumping from any current
version (like 1.0.0) to a target that violates the SemVer rule of consecutive versions (like 10.0.0).

### Long Term Support branches

The RepositoryInfo.xml file can define two properties:
  - `SingleMajor`: Setting this major number will only allow versions with this exact number as their Major.
  - `OnlyPatch`: Defaults to false. Sets it to true to allow only patches version (any version that bumps 
 the minor part will be forbidden). 

```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <SingleMajor>4</SingleMajor>
    <OnlyPatch>true</OnlyPatch>
  </SimpleGitVersion>
</RepositoryInfo>
```
This `SingleMajor` and `OnlyPatch` are enough to fully drive the behavior of SimpleGitVersion
regarding "[Long Term Support](https://en.wikipedia.org/wiki/Long-term_support)" application life cycle. 

### Branches that support CI-Builds

Specifying which branches should generate CI-Builds assemblies and/or packages is easy:

```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <Branches>
      <Branch Name="develop" CIVersionMode="LastReleaseBased" />
      <Branch Name="fx/new-way" VersionName="explore" CIVersionMode="ZeroTimed" />
    </Branches>
  </SimpleGitVersion>
</RepositoryInfo>
```
The `VersionName` and `CIVersionMode` are optional.

  - When specified `VersionName` overrides the branch `Name` as the CI-Build identifier. 
To be able to generate CI-Build versions that fit into NuGet V2 package name (CSemVer short form), the `VersionName` 
(or the branch `Name`) must not exceed 8 characters.
  - `CIVersionMode` can be:
    - `None`: This suppress any CI builds as if the ```<Branch>``` element was not specified.
    - `LastReleaseBased`: This is the default. Semantic versions computed in this mode are greater than the base release but lower than any subsequent release.
    - `ZeroTimed`: The computed semantic version is a 0.0.0 version with a suffix that makes it lower than any actual package (ie. lower than v0.0.0-alpha that is the very first possible CSemVer version).

---
**Why does [CSemVer](https://csemver.org) define `ZeroBased` for CI-Builds and SimpleGitVersion uses `ZeroTimed` term?**

The CSemVer's ZeroBased simply specifies that the **0.0.0** version must be used as the base version with a **-XXX** prerelease
but does not state what the **-XXX** is.

To support the short form, we must respect the 20 characters limit, SimpleGitVersion uses the number of seconds between the
commit's time and the 1<sup>st</sup> of january 2015, and this number of seconds is expressed in base 36 padded on 7 chars:
1000 years fit into 7 chars.

We also use `--` trick and append the branch name (that must not exceed 8 characters):

> 0.0.0--00yI6aT-develop

The code is [here](https://github.com/SimpleGitVersion/CSemVer-Net/blob/develop/CSemVer/CIBuildDescriptor.cs#L81).

---

### Ignoring local modifications to specific files
    
```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <IgnoreModifiedFiles>
      <Add>SharedKey.snk</Add>
      <Add>Common/Doc/ReleaseNotes.txt</Add>
    </IgnoreModifiedFiles>
  </SimpleGitVersion>
</RepositoryInfo>
```
This can be used if for any reason some files should (or can safely) be ignored in terms of
local changes: uncommitted changes for these files will be ignored. 

### Debugging

This is a dangerous option since it ignores all the locally modified files.

```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <Debug IgnoreDirtyWorkingFolder="true"/>
  </SimpleGitVersion>
</RepositoryInfo>
```

