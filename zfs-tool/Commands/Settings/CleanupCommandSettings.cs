using Spectre.Console.Cli;

namespace zfs_tool.Commands.Settings;

public class CleanupCommandSettings: AbstractCommandSettings
{
    [CommandOption("--keep")] public string Keep { get; set; } = "14d";
    
    // [CommandOption("--free")] public string Free { get; set; } = "0";

    [CommandOption("--format")] public string Format { get; set; } = "zfs destroy {name}";
    [CommandOption("--contains")] public string Contains { get; set; } = "";
    [CommandOption("--matches")] public string Matches { get; set; } = "";
    [CommandOption("--order-by")] public string OrderBy { get; set; } = "creation,name";


}