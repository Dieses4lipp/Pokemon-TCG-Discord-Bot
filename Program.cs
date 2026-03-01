using System.Diagnostics;
using Discord;
using Discord.Commands;
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
    public static CommandService Commands { get; set; } = default!;
    public static IServiceProvider Services { get; private set; } = default!;
    public static DateTime StartTime { get; private set; }

    /// <summary>
    ///     Restarts the bot by starting a new process and killing the current one.
    /// </summary>
    public static void RestartBot()
    {
        string fileName;
        try
        {
            fileName = _currentProcess.MainModule!.FileName;
        }
        catch (Exception)
        {
            fileName = "Unreadable-FileName";
        }
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
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
        // Load environment variables from .env file
        Env.Load();
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
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMessages |
                             GatewayIntents.MessageContent |
                             GatewayIntents.GuildMessageReactions,
            HandlerTimeout = null,
            ConnectionTimeout = 30000,
        };

        var client = new DiscordSocketClient(config);
        Commands = new CommandService(new CommandServiceConfig
        {
            DefaultRunMode = RunMode.Async,
            LogLevel = LogSeverity.Verbose
        });

        // Create the DI container and register services
        Services = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(Commands)
            .AddSingleton<InteractionHandler>()
            .BuildServiceProvider();

        var bot = new Bot(client);
        CommandHandler.ClearTradeSessions();
        // Register commands and start the bot
        await CommandHandler.RegisterCommandsAsync(Services);
        await bot.StartAsync(botToken);
        // Log the bot's start time and keep the application running
        StartTime = DateTime.UtcNow;
        Console.WriteLine($"Bot started at: {StartTime}");
        await Task.Delay(-1);
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