# SimpleGitVersion.MsBuild

This package sets the `Version`, `AssemblyVersion`, `FileVersion` and `InformationalVersion` MsBuild's properties
computed by [SimpleGitVersion.Core](https://github.com/CK-Build/SGV-Net/tree/master/SimpleGitVersion.Core) and,
by default, errors if the build `Configuration` is not the expected `Debug` or `Release` value.

To remove the `Configuration` check, use this property in your project:
```xml
<PropertyGroup>
    <SimpleGitVersionCheckBuildConfiguration>false</SimpleGitVersionCheckBuildConfiguration>
</PropertyGroup>
```

This package should be rarely used as it cannot set the build `Configuration`: this property is
initialized during the second step of the MsBuild's 6 steps evaluation phase (see [here](https://learn.microsoft.com/en-us/visualstudio/msbuild/build-process-overview))
in the `Microsoft.Common.CurrentVersion.targets` and its value is heavily used to derive a lot of fundamental aspect
of the build.

This is why this package can set the versions properties but only check that the actual build `Configuration` is
what it should be. In practice, an external process must computes the `Version`, `AssemblyVersion`, `FileVersion`,
`InformationalVersion` and `Configuration` and calls `msbuild` or the `dotnet` tool with theses properties as
parameters.

