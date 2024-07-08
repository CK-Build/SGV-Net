
# SimpleGitVersion.Core

Implements [SimpleGitVersion.Abstractions](../SimpleGitVersion.Abstractions) thanks to the excellent [LibGit2Sharp](https://www.nuget.org/packages/LibGit2Sharp).

The [CommitInfo](CommitInfo/CommitInfo.cs) is the main object of this library.
The hard work is done by the [TagCollector](TagCollector).

Simplest usage (other overloads exist):
```csharp
// path can be in any folder. The first parent with a /.git folder is found.
var info = CommitInfo.LoadFromPath( path );
```

Configurations can be done thanks to the optional **RepositoryInfo.xml** file that may appear at the root level in the repository.

## RepositoryInfo.xml: the &lt;SimpleGitVersion&gt; element

This file can contain a &lt;SimpleGitVersion&gt;...&lt;/SimpleGitVersion&gt; element that define options like
CI builds or StartingVersion that drives the behavior of *SimpleGitVersion.Core*.

### StartingVersion (attribute)

This defines the first version that should be considered in the repository:
```xml
<RepositoryInfo>
  <SimpleGitVersion StartingVersion="v0.6.0-rc">
  </SimpleGitVersion>
</RepositoryInfo>
```

When this version is specified, any existing commit tag with a version lower than it is simply ignored.
This is typically used to start using CSemVer on a repository that did not use it before and therefore
has incoherent or invalid tags.

Another common use is when for any reason one need to boost the actual version: jumping from any current
version (like 1.0.0) to a target that violates the SemVer rule of consecutive versions (like 10.0.0).

### UseReleaseBuildConfigurationFrom (attribute)

This configures the build configuration to use between "Debug" and "Release".
By default, it is the [ReleaseCandidate](https://github.com/CK-Build/CSemVer-Net/blob/master/CSemVer/PackageQuality.cs)
quality: only release candidates or stable versions will use "Release".
```xml
<RepositoryInfo>
  <SimpleGitVersion UseReleaseBuildConfigurationFrom="None"> <!-- Always uses "Debug" --> 
  </SimpleGitVersion>
</RepositoryInfo>
```

When specified, it must be:
 - `None`: always use "Debug" build configuration.
 - `CI`: always use "Release" build configuration.
 - `Exploratory`: always use "Release" except for CI builds.
 - `Preview`: use "Debug" for CI and Exploratory qualities, "Release" otherwise.
 - `ReleaseCandidate` or `rc`: this is the default.
 - `Stable`: only stable versions will use "Release", all prerelease versions will use "Debug".

This attribute can also be set at a branch level and overrides the top-level one:
```xml
<RepositoryInfo>
  <!-- Always uses "Release" except on "fx/new-way" where only release candidates
       or stable versions will use "Release". --> 
  <SimpleGitVersion UseReleaseBuildConfigurationFrom="CI">
    <Branches>
      <Branch Name="develop" CIVersionMode="LastReleaseBased" />
      <Branch Name="fx/new-way" VersionName="explore" CIVersionMode="ZeroTimed" UseReleaseBuildConfigurationFrom="ReleaseCandidate" />
    </Branches>
  </SimpleGitVersion>
</RepositoryInfo>
```

If for any reason "Debug" vs. "Release" is not enough, a simple extension point can be used to compute any esoteric build
configuration string. The build configuration is computed by the static `CommitInfo.BuildConfigurationSelector` function
that can be replaced. See [CommitInfo.FinalBuildInfo.cs](CommitInfo/CommitInfo.FinalBuildInfo.cs).

### Long Term Support (`SingleMajor` and `OnlyPatch` attributes)

The RepositoryInfo.xml file can define two attributes:
  - `SingleMajor`: Setting this major number will only allow versions with this exact number as their Major.
```xml
<RepositoryInfo>
  <SimpleGitVersion SingleMajor="4">
  </SimpleGitVersion>
</RepositoryInfo>
```

  - `OnlyPatch`: Defaults to false. Sets it to true to allow only patches version (any version that bumps 
 the Major or the Minor part will be forbidden). 
```xml
<RepositoryInfo>
  <SimpleGitVersion OnlyPatch="true">
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
    - `None`: This suppress any CI builds as if the `<Branch>` element was not specified.
    - `LastReleaseBased`: This is the default. Semantic versions computed in this mode are greater than the base release but lower than any subsequent release.
    - `ZeroTimed`: The computed semantic version is a 0.0.0 version with a suffix that makes it lower than any actual package (ie. lower than v0.0.0-alpha that is the very first possible CSemVer version).

---
**Why does [CSemVer](https://csemver.org) define `ZeroBased` for CI-Builds and SimpleGitVersion uses `ZeroTimed` term?**

The CSemVer's ZeroBased simply specifies that the **0.0.0** version must be used as the base version with a **-XXX** prerelease
but does not state what the **-XXX** is. SimpleGitVersion uses the date and time of the commit.

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

### Debugging & other advanced options

#### Debug element
This is a dangerous option since it ignores all the locally modified files.

```xml
<RepositoryInfo>
  <SimpleGitVersion>
    <Debug IgnoreDirtyWorkingFolder="true"/>
  </SimpleGitVersion>
</RepositoryInfo>
```

#### CheckExistingVersions attribute

When set to true, existing version tags will be checked.
Existing versions are filtered by `SingleMajor` if it is defined and the following checks are made:
- If there is a `StartingVersion` the first existing version must be this starting one.
- If there is no `StartingVersion` the first version must be one of the CSemVer's FirstPossibleVersions.
- Existing version tags must always be compact (no "holes" must exist between them).

These are really strong constraints and that's why this option defaults to false.

```xml
<RepositoryInfo>
  <SimpleGitVersion CheckExistingVersions="true">
  </SimpleGitVersion>
</RepositoryInfo>
```

#### RemoteName attribute

Defaults to "origin" (can never be null or empty) and should rarely be set: it is the name of the remote repository
that will be considered when working with branches.

```xml
<RepositoryInfo>
  <SimpleGitVersion RemoteName="dev-lead">
  </SimpleGitVersion>
</RepositoryInfo>
```
