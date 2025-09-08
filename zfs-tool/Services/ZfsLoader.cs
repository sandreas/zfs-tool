using CliWrap;
using CliWrap.Buffered;
using zfs_tool.Commands.Settings;
using zfs_tool.Enums;
using zfs_tool.Models;

namespace zfs_tool.Services;

public class ZfsLoader
{
    private readonly ZfsExecutor _zfs;
    private readonly ZfsParser _parser;
    private readonly ZfsVersionChecker _checker;
    public BufferedCommandResult LastResult { get; set; } = new(1, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, "", "Uninitialized class property, please call at least one method of ZfsLoader class");


    
    public ZfsLoader(ZfsExecutor zfs, ZfsVersionChecker checker, ZfsParser parser)
    {
        _zfs = zfs;
        _checker = checker;
        _parser = parser;
    }
    
    private static async Task<string?> GetMockFileContent(string key, CancellationToken cancellationToken)
    {
#if DEBUG
        var mockFile = "../../../samples/mock-" + key + ".txt";
        if (File.Exists(mockFile))
        {
            return await File.ReadAllTextAsync(mockFile, cancellationToken);
        }
#endif
        return null;
    }
    
    private void UpdateManualLastResult(string message = "Reset method in ZfsLoader did not cause a real result")
    {
        LastResult = new BufferedCommandResult(1, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, "",
            message);
    }

    public async Task<IEnumerable<ZfsSnapshot>> LoadSnapshots(ExtraZfsProperties extensions, CancellationToken cancellationToken)
    {
        UpdateManualLastResult();
        var commandOutput = await GetZfsCommandOutput("all-snapshots", [
            "list", "-t", "snapshot", "-o", "creation,name,written"
        ], cancellationToken);
        
        return string.IsNullOrEmpty(commandOutput) ? new List<ZfsSnapshot>() : _parser.ParseList(commandOutput);
    }

    
    public async Task<bool> LoadAllSnapshotReclaimsAsync(IEnumerable<ZfsSnapshot> snapshots, ExtraZfsProperties extensions, CancellationToken cancellationToken)
    {
        if (!extensions.HasFlag(ExtraZfsProperties.Reclaim) && !extensions.HasFlag(ExtraZfsProperties.ReclaimSum))
        {
            return false;
        }
        var groupedSnapshots = snapshots.GroupBy(snapshot => snapshot.Path).ToList();
        foreach (var group in groupedSnapshots)
        {
            if (!groupedSnapshots.Any())
            {
                continue;
            }

            var firstSnapshot = group.First();
            foreach (var snapshot in group)
            {
                var snapshotReclaims = await LoadSnapshotReclaimsAsync(snapshot, null, cancellationToken);
                var reclaimedBytes = _parser.ParseReclaimBytes(snapshotReclaims);
                if (reclaimedBytes >= 0)
                {
                    snapshot.ReclaimBytes = reclaimedBytes;
                }

                if (snapshot == firstSnapshot)
                {
                    snapshot.ReclaimSumBytes = snapshot.ReclaimBytes;
                    continue;
                }
                
                if (!extensions.HasFlag(ExtraZfsProperties.ReclaimSum))
                {
                    continue;
                }
                var snapshotSumReclaims = await LoadSnapshotReclaimsAsync(firstSnapshot, snapshot, cancellationToken);
                var sumReclaimedBytes = _parser.ParseReclaimBytes(snapshotSumReclaims);
                if (sumReclaimedBytes >= 0)
                {
                    snapshot.ReclaimSumBytes = sumReclaimedBytes;
                }
            }
        }

        return true;
    }
    
    
    public async Task<string> LoadSnapshotReclaimsAsync(ZfsSnapshot snapFrom, ZfsSnapshot? snapTo,
        CancellationToken cancellationToken)
    {
        UpdateManualLastResult();
        var reclaimSnapshotNameRange = snapFrom.FullName;
        if (snapTo is not null && snapFrom.FullName != snapTo.FullName)
        {
            if (!string.IsNullOrEmpty(snapFrom.Path) && snapFrom.Path != snapTo.Path)
            {
                UpdateManualLastResult("from snapshot and to snapshot are not on the same dataset");
                return "";
            }

            reclaimSnapshotNameRange = $"{snapFrom.Path}@{snapFrom.Name}%{snapTo.Name}";
        }

        var mockKey = "reclaim_" + reclaimSnapshotNameRange.Split('@').Last();
        return await GetZfsCommandOutput(mockKey, new[]
        {
            "destroy", "-nv", reclaimSnapshotNameRange
        }, cancellationToken);

    }

    private async Task<string> GetZfsCommandOutput(string mockKey, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        if (!await _checker.LoadVersionAsync(cancellationToken))
        {
            #if DEBUG
                return await GetMockFileContent(mockKey, cancellationToken) ?? "";
            #endif
            
            return "";
        }
     
        
        // to load reclaims you have to simulate a destroy (-n) and enable verbose output (-v)
        // this is 
        var environmentVariables = new Dictionary<string, string?> {{"LANG", "en"}};
        LastResult = await _zfs.ExecuteAsync(arguments, cancellationToken);
        return !LastResult.IsSuccess ? "" : LastResult.StandardOutput;
    }
}