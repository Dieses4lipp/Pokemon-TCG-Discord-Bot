using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot;

/// <summary>
///     The main entry point for the Discord bot, handles bot startup, command registration, and
///     event subscriptions.
/// </summary>
internal static class Program
{
    private static Process _currentProcess = default!;
    public static IServiceProvider Services { get; private set; } = default!;
    public static DateTime StartTime { get; private set; }

    /// <summary>
    ///     Restarts the bot by starting a new process and killing the current one.
    /// </summary>
    public static void RestartBot()
    {
        string fileName = Environment.ProcessPath ?? "dotnet";

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            // If it's a dll handled differently
            Arguments = fileName.EndsWith(".dll")
            ? $"{fileName} {string.Join(" ", Environment.GetCommandLineArgs().Skip(1))}"
            : string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
            UseShellExecute = false
        };

        Process.Start(startInfo);
        _currentProcess.Kill();
    }

    /// <summary>
    ///     Starts the bot, loads the environment variables, sets up the bot client, and registers commands.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static async Task RunBotAsync()
    {
        // Load environment variables from the nearest .env file in the working directory or above
        Env.TraversePath().Load();
        string? botToken = Environment.GetEnvironmentVariable("TOKEN");

        if (string.IsNullOrEmpty(botToken))
        {
            Console.WriteLine("Error: Token couldn't be read from .env file!");
            return;
        }

        Console.WriteLine("Starting Bot...");

        // Configure the Discord client
        var config = new DiscordSocketConfig
        {
            // GuildMembers is privileged: it must also be enabled in the Discord Developer Portal,
            // otherwise the login fails. It is needed for the UserLeft event.
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMembers,
            HandlerTimeout = null,
            ConnectionTimeout = 30000,
        };

        var client = new DiscordSocketClient(config);
        // Create the DI container and register services
        Services = new ServiceCollection()
            .AddSingleton(client)
            .BuildServiceProvider();

        var bot = new Bot(client);
        // Restore bot on/off, locked sets, pull count, trades and paid packs from before the restart
        BotStateStore.Load();
        _ = CardStorage.RunArchivePurgeLoopAsync();
        await bot.StartAsync(botToken);
        // Log the bot's start time and keep the application running until the watchdog exits it
        StartTime = DateTime.UtcNow;
        Console.WriteLine($"Bot started at: {StartTime}");
        await GatewayWatchdog.RunAsync(client);
    }

    /// <summary>
    ///     The main method that starts the bot asynchronously.
    /// </summary>
    private static async Task Main(string[] _)
    {
        _currentProcess = Process.GetCurrentProcess();
        await RunBotAsync();
    }
}