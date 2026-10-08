using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        }));
        var logger = loggerFactory.CreateLogger(typeof(Program));

        // Load environment variables from the nearest .env file in the working directory or above
        Env.TraversePath().Load();
        string? botToken = Environment.GetEnvironmentVariable("TOKEN");

        if (string.IsNullOrEmpty(botToken))
        {
            logger.LogCritical("Token couldn't be read from .env file!");
            return;
        }

        string? ownerIdText = Environment.GetEnvironmentVariable("OWNER_ID");
        ulong? ownerId = null;
        if (!string.IsNullOrWhiteSpace(ownerIdText))
        {
            if (!ulong.TryParse(ownerIdText, out ulong parsedOwnerId))
            {
                logger.LogCritical("OWNER_ID '{OwnerId}' is not a Discord user ID", ownerIdText);
                return;
            }
            ownerId = parsedOwnerId;
        }
        if (ownerId is null)
            logger.LogInformation("OWNER_ID not set, owner commands are limited to the Discord application owner");
        else
            logger.LogInformation("Owner commands are limited to user {OwnerId}", ownerId);

        logger.LogInformation("Starting bot");

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
            .AddSingleton(loggerFactory)
            .AddLogging()
            .AddSingleton(new BotOptions(botToken, ownerId))
            .AddSingleton(client)
            .AddSingleton(interactions)
            .AddSingleton<CardApiClient>()
            .AddSingleton<SetCardCache>()
            .AddSingleton<PackSettingsProvider>()
            .AddSingleton<UserRepository>()
            .AddSingleton<BotState>()
            .AddSingleton<SessionStore>()
            .AddSingleton<BotStateStore>()
            .AddSingleton<SessionCleanup>()
            .AddSingleton<Bot>()
            .BuildServiceProvider();

        // Restore bot on/off, locked sets, pull count, trades and paid packs from before the restart
        services.GetRequiredService<BotStateStore>().Load();
        _ = services.GetRequiredService<UserRepository>().RunArchivePurgeLoopAsync();
        _ = services.GetRequiredService<SessionCleanup>().RunAsync();
        await services.GetRequiredService<Bot>().StartAsync(botToken);
        // Log the bot's start time and keep the application running until the watchdog exits it
        logger.LogInformation("Bot started at {StartedAtUtc:O}", services.GetRequiredService<BotState>().StartedAtUtc);
        await GatewayWatchdog.RunAsync(client, loggerFactory.CreateLogger(typeof(GatewayWatchdog)));
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