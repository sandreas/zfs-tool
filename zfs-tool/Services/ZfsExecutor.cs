using CliWrap;
using CliWrap.Buffered;

namespace zfs_tool.Services;

public class ZfsExecutor
{
    private readonly string _command;
    private readonly IReadOnlyDictionary<string, string?> _envVars;

    public ZfsExecutor(string command="zfs", Dictionary<string, string?>? envVars = null)
    {
        _command = command;
        _envVars = envVars ?? new Dictionary<string, string?> {{"LANG", "en"}};
    }
    public async Task<BufferedCommandResult> ExecuteAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        return await Cli.Wrap(_command)
            .WithArguments(arguments)
            .WithEnvironmentVariables(_envVars)
            .ExecuteBufferedAsync(ct);
    }
}