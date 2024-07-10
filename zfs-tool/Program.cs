using Microsoft.Extensions.DependencyInjection;
using Sandreas.SpectreConsoleHelpers.DependencyInjection;
using Sandreas.SpectreConsoleHelpers.Services;
using Spectre.Console;
using Spectre.Console.Cli;
using zfs_tool.Commands;

try
{
    var debugMode = args.Contains("--debug");

    var settingsProvider = new CustomCommandSettingsProvider();

    var services = new ServiceCollection();
    services.AddSingleton(_ => settingsProvider);
    services.AddSingleton<SpectreConsoleService>();
    services.AddSingleton<CancellationTokenSource>();
    var app = new CommandApp(new CustomTypeRegistrar(services));

    app.Configure(config =>
    {
        config.SetInterceptor(new CustomCommandInterceptor(settingsProvider));
        config.UseStrictParsing();
        config.CaseSensitivity(CaseSensitivity.None);
        config.SetApplicationName("zfs-tool");
        config.SetApplicationVersion("0.0.1");
        config.ValidateExamples();
        config.AddCommand<CleanupCommand>("cleanup")
            .WithDescription("cleanup zfs snapshots")
            // .WithExample("dump", "--help")
            // .WithExample("dump", "input.mp3")
            // .WithExample("dump", "audio-directory/", "--include-extension", "m4b", "--include-extension", "mp3", "--format", "ffmetadata", "--include-property", "title", "--include-property", "artist")
            // .WithExample("dump", "input.mp3", "--format", "json", "--query", "$.meta.album")
            ;
        

        if (debugMode)
        {
            config.PropagateExceptions();
        }
#if DEBUG
        config.ValidateExamples();
#endif
    });

    return await app.RunAsync(args).ConfigureAwait(false);
}
catch (Exception e)
{
    if (e is CommandParseException { Pretty: { } } ce)
    {
        AnsiConsole.Write(ce.Pretty);
    }

    AnsiConsole.WriteException(e);
    // return (int)ReturnCode.UncaughtException;
    return 1;
}
finally
{
    // Log.CloseAndFlush();
}