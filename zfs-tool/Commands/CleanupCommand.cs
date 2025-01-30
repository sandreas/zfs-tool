using System.Text.RegularExpressions;
using Sandreas.SpectreConsoleHelpers.Commands;
using Sandreas.SpectreConsoleHelpers.Services;
using SmartFormat;
using Spectre.Console;
using Spectre.Console.Cli;
using zfs_tool.Commands.Settings;
using zfs_tool.Directives;
using zfs_tool.Models;
using zfs_tool.Services;

namespace zfs_tool.Commands;


public class CleanupCommand : CancellableAsyncCommand<CleanupCommandSettings>
{
    private readonly SpectreConsoleService _console;
    private readonly ZfsLoader _loader;
    private readonly CancellationTokenSource _cts;
    private readonly SmartFormatter _formatter;

    /*
    [GeneratedRegex("([0-9]+[dhmsfz])", RegexOptions.IgnoreCase, "de-DE")]
    private static partial Regex TimeSpanSplitRegex();
*/

    public CleanupCommand(SpectreConsoleService console, ZfsLoader loader, CancellationTokenSource cts, SmartFormatter formatter)
    {
        _console = console;
        _loader = loader;
        _cts = cts;
        _formatter = formatter;
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

        var snapshotsEnumerable = await _loader.LoadSnapshots(settings.LoadExtensionSettings, _cts.Token);
        var snapshots = snapshotsEnumerable.ToList();
        
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
        snapshotsToProcess = orderBy.Apply(snapshotsToProcess).ToList();

        var counter = 0;
        var formatTemplate = settings.Format;
        
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.FullName), s => s.FullName.Length, formatTemplate, snapshots);
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.Reclaim), s => s.Reclaim.Length, formatTemplate, snapshots);
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.ReclaimSum), s => s.ReclaimSum.Length, formatTemplate, snapshots);
        
        foreach (var snap in snapshotsToProcess)
        {
            var output = _formatter.Format(formatTemplate, snap);
            _console.WriteLine(output);
            counter++;
        }

        if (counter == 0)
        {
            _console.Error.WriteLine("no matching snapshots to process");
        }
        
        return 0;
    }

    private static string ReplacePaddedTemplateVariables(string varName, Func<ZfsSnapshot, int> selector, string formatTemplate, List<ZfsSnapshot> snapshots)
    {
        var paddedVarName = "{" + varName + "Padded}";
        if (!formatTemplate.Contains(paddedVarName))
        {
            return formatTemplate;
        }
        var padSize = snapshots.Max(selector) + 1;
        return formatTemplate.Replace(paddedVarName, "{"+varName+",-" + padSize + "}");
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