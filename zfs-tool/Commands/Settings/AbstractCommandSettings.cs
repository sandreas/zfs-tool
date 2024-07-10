using Spectre.Console.Cli;

namespace zfs_tool.Commands.Settings;

public abstract class AbstractCommandSettings : CommandSettings
{
    [CommandOption("--debug")] public bool Debug { get; set; } = false;
    [CommandOption("--force")] public bool Force { get; set; } = false;
}