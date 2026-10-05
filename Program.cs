using System.Diagnostics;
using Discord;
using Discord.Interactions;
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

        string? ownerIdText = Environment.GetEnvironmentVariable("OWNER_ID");
        ulong? ownerId = null;
        if (!string.IsNullOrWhiteSpace(ownerIdText))
        {
            if (!ulong.TryParse(ownerIdText, out ulong parsedOwnerId))
            {
                Console.WriteLine($"Error: OWNER_ID '{ownerIdText}' is not a Discord user ID!");
                return;
            }
            ownerId = parsedOwnerId;
        }
        Console.WriteLine(ownerId is null
            ? "OWNER_ID not set, owner commands are limited to the Discord application owner."
            : $"Owner commands are limited to user {ownerId}.");

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

        // Sync run mode keeps handling interactions one at a time, as the client events did
        // before: user collections are plain JSON files without any locking
        var interactions = new InteractionService(client, new InteractionServiceConfig
        {
            DefaultRunMode = RunMode.Sync,
            LogLevel = LogSeverity.Info,
        });

        var services = new ServiceCollection()
            .AddSingleton(new BotOptions(botToken, ownerId))
            .AddSingleton(client)
            .AddSingleton(interactions)
            .AddSingleton<CardApiClient>()
            .AddSingleton<SetCardCache>()
            .AddSingleton<UserRepository>()
            .AddSingleton<BotState>()
            .AddSingleton<SessionStore>()
            .AddSingleton<BotStateStore>()
            .AddSingleton<Bot>()
            .BuildServiceProvider();

        // Restore bot on/off, locked sets, pull count, trades and paid packs from before the restart
        services.GetRequiredService<BotStateStore>().Load();
        _ = services.GetRequiredService<UserRepository>().RunArchivePurgeLoopAsync();
        await services.GetRequiredService<Bot>().StartAsync(botToken);
        // Log the bot's start time and keep the application running until the watchdog exits it
        Console.WriteLine($"Bot started at: {services.GetRequiredService<BotState>().StartedAtUtc}");
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