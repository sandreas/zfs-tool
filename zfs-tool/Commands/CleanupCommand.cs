using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using NeoSmart.PrettySize;
using Sandreas.SpectreConsoleHelpers.Commands;
using Sandreas.SpectreConsoleHelpers.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using zfs_tool.Commands.Settings;
using zfs_tool.Directives;
using zfs_tool.Models;
using zfs_tool.Parsers;

namespace zfs_tool.Commands;


public partial class CleanupCommand : CancellableAsyncCommand<CleanupCommandSettings>
{
    private readonly SpectreConsoleService _console;

    /*
    [GeneratedRegex("([0-9]+[dhmsfz])", RegexOptions.IgnoreCase, "de-DE")]
    private static partial Regex TimeSpanSplitRegex();
*/

    public CleanupCommand(SpectreConsoleService console)
    {
        _console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CleanupCommandSettings settings,
        CancellationToken cancellationToken)
    {

        var keep = ParseKeepValue(settings.Keep);
        if (keep == TimeSpan.MaxValue)
        {
            _console.Error.WriteLine("Could not parse value for --keep");
            return 1;
        }

        var snapshotFile = Environment.GetEnvironmentVariable("ZFS_TOOL_SNAPSHOT_FILE");
        string snapList;
        if (snapshotFile == null)
        {
            var result = await Cli.Wrap("zfs")
                .WithArguments(new string[]
                {
                    "list", "-t", "snapshot", "-o", "creation,name,written"
                })
                .ExecuteBufferedAsync(cancellationToken);


            if (!result.IsSuccess)
            {
                _console.Error.WriteLine($"zfs - could not read snapshots: {result.StandardError}{result.StandardOutput} ({result.ExitCode})");
                return 1;
            }
            snapList = result.StandardOutput;
        }
        else
        {
            snapList = await File.ReadAllTextAsync(snapshotFile, cancellationToken);
        }
        
        var parser = new ZfsParser();
        var snapshots = parser.ParseList(snapList).ToList();
        if (snapshots.Count == 0)
        {
            _console.Error.WriteLine("no snapshots found");
            return 1;
        }
        var keepDate = DateTime.Now - keep;
        
        var snapshotsToProcess = snapshots
            .Where(s => keepDate >= s.Creation);

        if (!string.IsNullOrEmpty(settings.Contains))
        {
            snapshotsToProcess = snapshotsToProcess.Where(s => s.Name.Contains(settings.Contains));
        }
        
        if (!string.IsNullOrEmpty(settings.Matches))
        {
            if (!IsValidRegex(settings.Matches))
            {
                _console.Error.WriteLine($"Invalid regex: {settings.Matches}");
                return 1;
            }
            snapshotsToProcess = snapshotsToProcess.Where(s => Regex.Match(s.Name, settings.Matches).Success);
        }

        var orderBy = new OrderByDirective<ZfsSnapshot>(settings.OrderBy, OrderByHandler);
        snapshotsToProcess = orderBy.Apply(snapshotsToProcess);

        var counter = 0;
        foreach (var snap in snapshotsToProcess)
        {
            var outputString = settings.Format
                .Replace("{name}", snap.Name)
                .Replace("{creation}", snap.Creation.ToString(CultureInfo.InvariantCulture))
                .Replace("{written}", new PrettySize(snap.WrittenBytes).ToString())
                ;
            
            _console.WriteLine(outputString);
            counter++;
        }

        if (counter == 0)
        {
            _console.Error.WriteLine("no matching snapshots to process");
        }
        
        return 0;
    }

    private static Func<ZfsSnapshot, IComparable> OrderByHandler(string field) => field.ToLowerInvariant() switch
    {
        "written" => f => f.WrittenBytes,
        "creation" => f => f.Creation,
        _ => f => f.Name
    };
    

    private static bool IsValidRegex(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        try
        {
            _ = Regex.Match("", pattern);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return true;
    }
    
    private static TimeSpan ParseKeepValue(string keepValue)
    {
        // System.Xml.XmlConvert.ToTimeSpan("P0DT1H0M0S")
        if (int.TryParse(keepValue, out var keepDays))
        {
            return TimeSpan.FromDays(keepDays);
        }

        var regex = new Regex("([0-9]+[dhmsfz])", RegexOptions.IgnoreCase);
        var substrings = regex.Split(keepValue.ToLowerInvariant()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return substrings.Count == 0 ? TimeSpan.MaxValue : substrings.Aggregate(TimeSpan.Zero, (current, sub) => current + ConvertToTimeSpan(sub));
    }
    
    private static TimeSpan ConvertToTimeSpan(string timeSpan)
    {
        var l = timeSpan.Length - 1;
        var value = timeSpan.Substring(0, l);
        var type = timeSpan.Substring(l, 1);

        return type switch
        {
            "d" => TimeSpan.FromDays(double.Parse(value)),
            "h" => TimeSpan.FromHours(double.Parse(value)),
            "m" => TimeSpan.FromMinutes(double.Parse(value)),
            "s" => TimeSpan.FromSeconds(double.Parse(value)),
            "f" => TimeSpan.FromMilliseconds(double.Parse(value)),
            "z" => TimeSpan.FromTicks(long.Parse(value)),
            _ => TimeSpan.FromDays(double.Parse(value))
        };
    }


}