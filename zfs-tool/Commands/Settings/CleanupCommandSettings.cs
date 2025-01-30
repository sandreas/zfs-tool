using Spectre.Console.Cli;

namespace zfs_tool.Commands.Settings;
[Flags]
public enum LoadExtensions
{
    None = 1 << 0,
    Reclaim = 1 << 1,
    ReclaimSum = 1 << 2,
    All = int.MaxValue
}
public class CleanupCommandSettings: AbstractCommandSettings
{
    [CommandOption("--keep")] public string Keep { get; set; } = "30d";
    
    // [CommandOption("--free")] public string Free { get; set; } = "0";

    [CommandOption("--format")] public string Format { get; set; } = "{Creation} {FullNamePadded} {ReclaimedPadded} {ReclaimedSumPadded}";
    [CommandOption("--contains")] public string Contains { get; set; } = "";
    [CommandOption("--matches")] public string Matches { get; set; } = "";
    [CommandOption("--order-by")] public string OrderBy { get; set; } = "path,creation,name";
    [CommandOption("--load-extensions")] public LoadExtensions LoadExtensionSettings { get; set; } = LoadExtensions.None;
}