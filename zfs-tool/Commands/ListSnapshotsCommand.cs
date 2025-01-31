using System.Text.RegularExpressions;
using Sandreas.SpectreConsoleHelpers.Commands;
using Sandreas.SpectreConsoleHelpers.Services;
using SmartFormat;
using Spectre.Console;
using Spectre.Console.Cli;
using zfs_tool.Commands.Settings;
using zfs_tool.Directives;
using zfs_tool.Enums;
using zfs_tool.Models;
using zfs_tool.Services;

namespace zfs_tool.Commands;


public class ListSnapshotsCommand : CancellableAsyncCommand<ListSnapshotsCommandSettings>
{
    private readonly SpectreConsoleService _console;
    private readonly ZfsLoader _loader;
    private readonly CancellationTokenSource _cts;
    private readonly SmartFormatter _formatter;

    /*
    [GeneratedRegex("([0-9]+[dhmsfz])", RegexOptions.IgnoreCase, "de-DE")]
    private static partial Regex TimeSpanSplitRegex();
*/

    public ListSnapshotsCommand(SpectreConsoleService console, ZfsLoader loader, CancellationTokenSource cts, SmartFormatter formatter)
    {
        _console = console;
        _loader = loader;
        _cts = cts;
        _formatter = formatter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ListSnapshotsCommandSettings settings,
        CancellationToken cancellationToken)
    {



        var snapshotsEnumerable = await _loader.LoadSnapshots(settings.ExtraProperties, _cts.Token);
        var snapshots = snapshotsEnumerable.ToList();
        
        if (snapshots.Count == 0)
        {
            _console.Error.WriteLine("no snapshots found");
            return 1;
        }
        
        var snapshotsToProcess = snapshots;


        // required space parameter has to determine reclaims, so these properties must be loaded
        if (!string.IsNullOrEmpty(settings.RequiredSpace))
        {
            settings.ExtraProperties |= ExtraZfsProperties.All;
        }

        if (!string.IsNullOrEmpty(settings.KeepTime))
        {
            var keep = ParseKeepValue(settings.KeepTime);
            if (keep == TimeSpan.MaxValue)
            {
                _console.Error.WriteLine("Could not parse value for --keep-time");
                return 1;
            }
            var keepDate = DateTime.Now - keep;

            snapshotsToProcess = snapshotsToProcess.Where(s => keepDate >= s.Creation).ToList();
        }

        if (!string.IsNullOrEmpty(settings.Contains))
        {
            snapshotsToProcess = snapshotsToProcess.Where(s => s.FullName.Contains(settings.Contains)).ToList();
        }

        snapshotsToProcess = snapshotsToProcess.ToList();
        await _loader.LoadAllSnapshotReclaimsAsync(snapshotsToProcess, settings.ExtraProperties, _cts.Token);

        if (!string.IsNullOrEmpty(settings.Matches))
        {
            if (!IsValidRegex(settings.Matches))
            {
                _console.Error.WriteLine($"Invalid regex: {settings.Matches}");
                return 1;
            }
            snapshotsToProcess = snapshotsToProcess.Where(s => Regex.Match(s.Name, settings.Matches).Success).ToList();
        }

        if (settings.Limit > 0 && snapshotsToProcess.Count > 0)
        {
            var snapGroups = snapshotsToProcess.GroupBy(s => s.Path);
            var limited = new List<ZfsSnapshot>();
            foreach (var group in snapGroups)
            {
                limited.AddRange(group.Take(settings.Limit));
            }

            if (!limited.Any())
            {
                _console.Error.WriteLine($"Limit resulted in empty list:");
                return 1;
            }

            snapshotsToProcess = limited;
        }
        
        
        if (!string.IsNullOrEmpty(settings.RequiredSpace))
        {
            if (settings.Format == ListSnapshotsCommandSettings.DefaultFormat)
            {
                settings.Format = ListSnapshotsCommandSettings.DefaultRequiredSpaceFormat;
            }
            if (!ZfsParser.TryParseSize(settings.RequiredSpace.ToLowerInvariant(), out var requiredSpace))
            {
                _console.Error.WriteLine("Could not parse value for --required-space");
                return 1;
            }

            
            var snapshotsToDestroy = new List<ZfsSnapshot>();
            var aquiredSpace = 0L;
            var snapGroups = new Dictionary<string, ZfsSnapshot>();
            while (snapshotsToProcess.Count > 0 && aquiredSpace < requiredSpace)
            {
                var oldestSnapshot = snapshotsToProcess.MinBy(s => s.Creation);
                if (oldestSnapshot == null)
                {
                    _console.Error.WriteLine("Could not determine oldest snapshot");
                    return 1;
                }

                snapGroups[oldestSnapshot.Path] = oldestSnapshot;
                snapshotsToDestroy.Add(oldestSnapshot);
                aquiredSpace = snapGroups.Sum(kvp => kvp.Value.ReclaimSumBytes);
                snapshotsToProcess.Remove(oldestSnapshot);
            }

            if (aquiredSpace < requiredSpace)
            {
                _console.Error.WriteLine($"Not enough snapshots to aquire required space of ${settings.RequiredSpace}");
                return 1;
            }

            snapshotsToProcess = snapshotsToDestroy;
        }
        
        
        

        var orderBy = new OrderByDirective<ZfsSnapshot>(settings.OrderBy, OrderByHandler);
        snapshotsToProcess = orderBy.Apply(snapshotsToProcess).ToList();

        var counter = 0;
        var formatTemplate = settings.Format;
        
        if(formatTemplate == ListSnapshotsCommandSettings.DefaultFormat 
           && (settings.ExtraProperties.HasFlag(ExtraZfsProperties.Reclaim) || settings.ExtraProperties.HasFlag(ExtraZfsProperties.ReclaimSum)))
        {
            formatTemplate = ListSnapshotsCommandSettings.DefaultReclaimFormat;
        }
        
        
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.FullName), s => s.FullName.Length, formatTemplate, snapshots);
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.Written), s => s.Written.Length, formatTemplate, snapshots);
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.Reclaim), s => s.Reclaim.Length, formatTemplate, snapshots);
        formatTemplate = ReplacePaddedTemplateVariables(nameof(ZfsSnapshot.ReclaimSum), s => s.ReclaimSum.Length, formatTemplate, snapshots);
        
        foreach (var snap in snapshotsToProcess)
        {
            try
            {
                var output = _formatter.Format(formatTemplate, snap);
                if (string.IsNullOrWhiteSpace(output))
                {
                    WriteInvalidFormatError("the format string does not produce any readable output", settings.Format, formatTemplate);
                    return 1;
                }
                _console.WriteNoBreakLine(output);
                counter++;
            }
            catch (Exception e)
            {

                WriteInvalidFormatError("the format string is invalid", settings.Format, formatTemplate);
                if (settings.Debug)
                {
                    _console.Error.WriteException(e);    
                }
                return 1;
            }

        }

        if (counter == 0)
        {
            _console.Error.WriteLine("no matching snapshots to process");
        }
        
        return 0;
    }

    private void WriteInvalidFormatError(string message, string formatString, string compiledFormatString)
    {
        _console.Error.WriteLine($"Formatter error - {message}: \"{formatString}\"");
        _console.Error.WriteLine($"  compiled: \"{compiledFormatString}\"");
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
        "creation" => f => f.Creation,
        "path" => f => f.Path,
        "name" => f => f.Name,
        "fullname" => f => f.FullName,
        "written" => f => f.WrittenBytes,
        "reclaim" => f => f.ReclaimBytes,
        "reclaimsum" => f => f.ReclaimSumBytes,
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