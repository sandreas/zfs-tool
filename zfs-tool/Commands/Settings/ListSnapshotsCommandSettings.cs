using Spectre.Console.Cli;
using zfs_tool.Enums;

namespace zfs_tool.Commands.Settings;

public class ListSnapshotsCommandSettings: AbstractCommandSettings
{
    public const string DefaultFormat = "{Creation:yyyy-MM-dd HH\\:mm} {FullNamePadded} {WrittenPadded}";
    public const string DefaultReclaimFormat = "{Creation:yyyy-MM-dd HH\\:mm} {FullNamePadded} {WrittenPadded} {ReclaimPadded} {ReclaimSumPadded}";
    public const string DefaultRequiredSpaceFormat = "zfs destroy {FullName}     # {Creation:yyyy-MM-dd HH\\:mm}  rcl: {ReclaimPadded} agg: {ReclaimSumPadded}";

    [CommandOption("--keep-time")] public string KeepTime { get; set; } = "30d";
    [CommandOption("--format")] public string Format { get; set; } = DefaultFormat;
    [CommandOption("--contains")] public string Contains { get; set; } = "";
    [CommandOption("--matches")] public string Matches { get; set; } = "";
    [CommandOption("--order-by")] public string OrderBy { get; set; } = "path,creation,name";
    [CommandOption("--limit")] public int Limit { get; set; } = 0;
    
    [CommandOption("--required-space")] public string RequiredSpace { get; set; } = "";
    [CommandOption("--extra-properties")] public ExtraZfsProperties ExtraProperties { get; set; } = ExtraZfsProperties.None;
}