using CliWrap;
using CliWrap.Buffered;

namespace zfs_tool.Services;

public class ZfsVersionChecker
{
    private readonly ZfsExecutor _zfs;
    public bool? IsZfsAvailable { get; set; }

    public ZfsVersionChecker(ZfsExecutor zfs)
    {
        _zfs = zfs;
    }
    public async Task<bool> LoadVersionAsync(CancellationToken ct)
    {
        if (IsZfsAvailable != null)
        {
            return await Task.FromResult(IsZfsAvailable ?? false);
        }
        var result = await _zfs.ExecuteAsync(["--version"], ct);
        if (result.ExitCode != 0)
        {
            IsZfsAvailable = false;
            return false;
        }

        // var output = result.StandardOutput;
        IsZfsAvailable = true;
        return true;

    }
}