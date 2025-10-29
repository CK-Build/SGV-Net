using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.IO;

namespace SimpleGitVersion.MsBuild;

public class GetSimpleGitVersionTask : Task
{
    [Required]
    public string SGVFilePath { get; set; }

    [Output]
    public string Version { get; set; }

    [Output]
    public string AssemblyVersion { get; set; }

    [Output]
    public string FileVersion { get; set; }

    [Output]
    public string InformationalVersion { get; set; }

    [Output]
    public string BuildConfiguration { get; set; }

    [Output]
    public string RemoteUrl { get; set; }

    public override bool Execute()
    {
        var lines = File.ReadAllLines( SGVFilePath );
        Version = lines[0];
        AssemblyVersion = lines[1];
        FileVersion = lines[2];
        InformationalVersion = lines[3];
        BuildConfiguration = lines[4];
        RemoteUrl = lines[5];
        return true;
    }
}
