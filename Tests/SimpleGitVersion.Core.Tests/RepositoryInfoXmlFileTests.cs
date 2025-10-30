using CSemVer;
using AwesomeAssertions;
using NUnit.Framework;
using System.Xml.Linq;

namespace SimpleGitVersion.Core.Tests;

[TestFixture]
public class RepositoryInfoXmlFileTests
{
    [Test]
    public void RepositoryInfoOptions_with_all_options()
    {
        var xOptions = XElement.Parse( @"
<SimpleGitVersion
        StartingVersion=""v4.2.0""
        SingleMajor=""3""
        OnlyPatch=""true""
        RemoteName=""not-the-origin""
        IgnoreAlreadyExistingVersion=""true""
        CheckExistingVersions=""true""
        UseReleaseBuildConfigurationFrom=""Exploratory"">
  <Debug IgnoreDirtyWorkingFolder=""true"" />
  <IgnoreModifiedFiles>
    <Add>SharedKey.snk</Add>
  </IgnoreModifiedFiles>
  <Branches>
    <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />
    <Branch Name=""fx/command-rework"" CIVersionMode=""ZeroTimed"" VersionName=""explo"" UseReleaseBuildConfigurationFrom=""CI"" />
    <Branch Name=""other"" CIVersionMode=""None"" UseReleaseBuildConfigurationFrom=""None"" />
  </Branches>
</SimpleGitVersion>" );

        var opt = new RepositoryInfoOptions( xOptions );
        var opt2 = new RepositoryInfoOptions( opt.ToXml() );
        opt2.Should().BeEquivalentTo( opt );

        opt.StartingVersion.Should().Be( "v4.2.0" );
        opt.SingleMajor.Should().Be( 3 );
        opt.OnlyPatch.Should().BeTrue();
        opt.UseReleaseBuildConfigurationFrom.Should().Be( MinPackageQuality.Exploratory );
        opt.RemoteName.Should().Be( "not-the-origin" );
        opt.IgnoreAlreadyExistingVersion.Should().BeTrue();
        opt.CheckExistingVersions.Should().BeTrue();
        opt.IgnoreDirtyWorkingFolder.Should().BeTrue();
        opt.IgnoreModifiedFiles.Should().BeEquivalentTo( "SharedKey.snk" );
        opt.Branches.Should().HaveCount( 3 );
        opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "develop", CIBranchVersionMode.LastReleaseBased ) );
        opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "fx/command-rework", CIBranchVersionMode.ZeroTimed, "explo" ) { UseReleaseBuildConfigurationFrom = MinPackageQuality.CI } );
        opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "other", CIBranchVersionMode.None ) { UseReleaseBuildConfigurationFrom = MinPackageQuality.None} );
    }

}
