using System.Diagnostics;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
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
        ///     Starts the bot, loads the environment variables, sets up the bot client, and
        ///     registers commands.
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
                                 GatewayIntents.GuildMessageReactions
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
            // Retrieve CommandsModule from DI container and register events
            client.ReactionAdded += (cache, channel, reaction) =>
            {
                _ = Task.Run(async () => await CommandHandler.HandleReactionAdded(cache, channel, reaction));
                return Task.CompletedTask;
            };
            client.MessageReceived += HandleCommandAsync;
            client.UserLeft += CommandHandler.HandleUserLeft;
            client.SelectMenuExecuted += InteractionHandler.HandleSelectMenu;
            StartTime = DateTime.UtcNow;
            Console.WriteLine($"Bot started at: {StartTime}");
            // Register commands and start the bot
            await CommandHandler.RegisterCommandsAsync(Services);
            await bot.StartAsync(botToken);
        }

        /// <summary>
        ///     Processes an incoming Discord message and executes a command asynchronously if the
        ///     message contains a valid command prefix.
        /// </summary>
        /// <param name="arg">
        ///     The message received from the Discord socket to be evaluated for command execution.
        /// </param>
        /// <returns>
        ///     A task that represents the asynchronous operation of handling the command message.
        /// </returns>
        private static async Task HandleCommandAsync(SocketMessage arg)
        {
            // Ignore system messages or messages from other bots
            if (arg is not SocketUserMessage message || message.Author.IsBot) return;

            int argPos = 0;

            if (message.HasStringPrefix("!", ref argPos))
            {
                var context = new SocketCommandContext(Services.GetRequiredService<DiscordSocketClient>(), message);

                // Execute the command in a background thread (non-blocking)
                _ = Task.Run(async () =>
                {
                    var result = await Commands.ExecuteAsync(context, argPos, Services);
                    if (!result.IsSuccess)
                    {
                        Console.WriteLine($"Error: {result.ErrorReason}");
                    }
                });
            }
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
}