using CSemVer;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Xml.Linq;
using System.Xml.Schema;

namespace SimpleGitVersion.Core.Tests
{
    [TestFixture]
    public class RepositoryInfoXmlFileTests
    {

        [Test]
        public void OLD_FORMAT_reading_repository_info_xml_file_StartingVersionForCSemVer_and_IgnoreModifiedFiles()
        {
            string s =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RepositoryInfo xmlns=""http://csemver.org/schemas/2015"">
	<StartingVersionForCSemVer>v4.2.0</StartingVersionForCSemVer>
    <IgnoreModifiedFiles>
        <Add>SharedKey.snk</Add>
    </IgnoreModifiedFiles>
</RepositoryInfo>";
            XDocument d = XDocument.Parse( s );

            RepositoryInfoOptions opt = new RepositoryInfoOptions( d.Root );

            opt.XmlMigrationRequired.Should().BeTrue();
            Assert.That( opt.Branches, Is.Empty );
            Assert.That( opt.StartingVersion, Is.EqualTo( "v4.2.0" ) );
            Assert.That( opt.HeadCommit, Is.Null );
            opt.IgnoreModifiedFiles.Should().BeEquivalentTo( ["SharedKey.snk"] );

            var expected = XElement.Parse( @"
<SimpleGitVersion StartingVersion=""v4.2.0"">
  <IgnoreModifiedFiles>
    <Add>SharedKey.snk</Add>
  </IgnoreModifiedFiles>
  <Branches />
</SimpleGitVersion>" );
            XElement.DeepEquals( opt.ToXml(), expected ).Should().BeTrue();
        }

        [Test]
        public void OLD_FORMAT_reading_repository_info_xml_file_Branches()
        {
            string s =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RepositoryInfo xmlns=""http://csemver.org/schemas/2015"">
    <Branches>
        <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />
        <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""Preview"" />
    </Branches>
</RepositoryInfo>";
            XDocument d = XDocument.Parse( s );
            RepositoryInfoOptions opt = new RepositoryInfoOptions( d.Root );

            Assert.That( opt.StartingVersion, Is.Null );
            Assert.That( opt.IgnoreModifiedFiles, Is.Empty );
            Assert.That( opt.Branches.Count, Is.EqualTo( 2 ) );

            Assert.That( opt.Branches[0].Name, Is.EqualTo( "develop" ) );
            Assert.That( opt.Branches[0].CIVersionMode, Is.EqualTo( CIBranchVersionMode.LastReleaseBased ) );
            Assert.That( opt.Branches[0].VersionName, Is.Null );

            Assert.That( opt.Branches[1].Name, Is.EqualTo( "exploratory" ) );
            Assert.That( opt.Branches[1].CIVersionMode, Is.EqualTo( CIBranchVersionMode.ZeroTimed ) );
            Assert.That( opt.Branches[1].VersionName, Is.EqualTo( "Preview" ) );

            var expected = XElement.Parse( @"
<SimpleGitVersion>
    <Branches>
        <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />
        <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""Preview"" />
    </Branches>
</SimpleGitVersion>" );

            XElement.DeepEquals( opt.ToXml(), expected ).Should().BeTrue();

        }

        [Test]
        public void OLD_FORMAT_full_repository_info_to_xml_is_valid_according_to_schema()
        {
            string oldString =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<RepositoryInfo xmlns=""http://csemver.org/schemas/2015"">
    <Debug IgnoreDirtyWorkingFolder=""true"" />
    <Branches>
        <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />
        <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""Preview"" />
        <Branch Name=""other"" CIVersionMode=""None"" />
    </Branches>
	<StartingVersionForCSemVer>v4.2.0</StartingVersionForCSemVer>
    <OnlyPatch>true</OnlyPatch>
    <SingleMajor>3</SingleMajor>
    <IgnoreModifiedFiles>
        <Add>SharedKey.snk</Add>
    </IgnoreModifiedFiles>
	<RemoteName>not-the-origin</RemoteName>
</RepositoryInfo>";
            XDocument dOld = XDocument.Parse( oldString );
            RepositoryInfoOptions oldOpt = new RepositoryInfoOptions( dOld.Root );

            XDocument d2Old = new XDocument( oldOpt.ToXml() );
            RepositoryInfoOptions oldOpt2 = new RepositoryInfoOptions( d2Old.Root );

            Assert.That( oldOpt.IgnoreDirtyWorkingFolder, Is.EqualTo( oldOpt2.IgnoreDirtyWorkingFolder ) );
            Assert.That( oldOpt.RemoteName, Is.EqualTo( oldOpt2.RemoteName ) );
            Assert.That( oldOpt.StartingVersion, Is.EqualTo( oldOpt2.StartingVersion ) );
            Assert.That( oldOpt.Branches.Count, Is.EqualTo( oldOpt2.Branches.Count ) );
            Assert.That( oldOpt.IgnoreModifiedFiles.Count, Is.EqualTo( oldOpt2.IgnoreModifiedFiles.Count ) );
            Assert.That( oldOpt.OnlyPatch, Is.True );
            Assert.That( oldOpt.SingleMajor, Is.EqualTo( 3 ) );


            var xOptions = XElement.Parse( @"
<SimpleGitVersion StartingVersion=""v4.2.0"" SingleMajor=""3"" OnlyPatch=""true"" RemoteName=""not-the-origin"">
  <Debug IgnoreDirtyWorkingFolder=""true"" />
  <IgnoreModifiedFiles>
    <Add>SharedKey.snk</Add>
  </IgnoreModifiedFiles>
  <Branches>
    <Branch Name=""develop"" CIVersionMode=""LastReleaseBased"" />
    <Branch Name=""exploratory"" CIVersionMode=""ZeroTimed"" VersionName=""Preview"" />
    <Branch Name=""other"" CIVersionMode=""None"" />
  </Branches>
</SimpleGitVersion>" );

            XElement.DeepEquals( oldOpt.ToXml(), xOptions ).Should().BeTrue();

            var opt = new RepositoryInfoOptions( xOptions );
            var opt2 = new RepositoryInfoOptions( opt.ToXml() );
            opt2.Should().BeEquivalentTo( opt );

        }

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
    <Branch Name=""other"" CIVersionMode=""None"" />
  </Branches>
</SimpleGitVersion>" );

            var opt = new RepositoryInfoOptions( xOptions );
            var opt2 = new RepositoryInfoOptions( opt.ToXml() );
            opt2.Should().BeEquivalentTo( opt );

            opt.StartingVersion.Should().Be( "v4.2.0" );
            opt.SingleMajor.Should().Be( 3 );
            opt.OnlyPatch.Should().BeTrue();
            opt.UseReleaseBuildConfigurationFrom.Should().Be( PackageQuality.Exploratory );
            opt.RemoteName.Should().Be( "not-the-origin" );
            opt.IgnoreAlreadyExistingVersion.Should().BeTrue();
            opt.CheckExistingVersions.Should().BeTrue();
            opt.IgnoreDirtyWorkingFolder.Should().BeTrue();
            opt.IgnoreModifiedFiles.Should().BeEquivalentTo( "SharedKey.snk" );
            opt.Branches.Should().HaveCount( 3 );
            opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "develop", CIBranchVersionMode.LastReleaseBased ) );
            opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "fx/command-rework", CIBranchVersionMode.ZeroTimed, "explo" ) { UseReleaseBuildConfigurationFrom = PackageQuality.CI } );
            opt.Branches.Should().ContainEquivalentOf( new RepositoryInfoOptionsBranch( "other", CIBranchVersionMode.None ) );
        }

    }
}
