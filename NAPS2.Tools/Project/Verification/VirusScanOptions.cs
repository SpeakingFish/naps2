using CommandLine;

namespace NAPS2.Tools.Project.Verification;

[Verb("virus", HelpText = "Scan the packaged app for antivirus false positives, 'virus {all|exe|msi|zip}'")]
public class VirusScanOptions : OptionsBase
{
    [Value(0, MetaName = "build type", Required = true, HelpText = "all|exe|msi|zip")]
    public string? BuildType { get; set; }
    
    [Option('p', "platform", Required = false, HelpText = "win|win32|win64|mac|macintel|macarm|linux")]
    public string? Platform { get; set; }
}