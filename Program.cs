using System.Diagnostics;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.Core;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
    /// <summary>
    ///     The main entry point for the Discord bot, handles bot startup, command
    ///     registration, and event subscriptions.
    /// </summary>
    class Program
    {
        private static Process _currentProcess;
        public static CommandService Commands { get; set; }
        public static IServiceProvider Services { get; private set; }


        /// <summary>
        ///     The main method that starts the bot asynchronously.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            _currentProcess = Process.GetCurrentProcess();
            await new Program().RunBotAsync();
        }
        /// <summary>
        ///    Restarts the bot by starting a new process and killing the current one.
        /// </summary>
        public static void RestartBot()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _currentProcess.MainModule.FileName,
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1)),
                UseShellExecute = false
            };
            Process.Start(startInfo);
            _currentProcess.Kill();
        }
        /// <summary>
        ///     Starts the bot, loads the environment variables, sets up the bot client,
        ///     and registers commands.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task RunBotAsync()
        {
            // Load environment variables from .env file
            Env.Load();
            string botToken = Environment.GetEnvironmentVariable("TOKEN");

            if (string.IsNullOrEmpty(botToken))
            {
                Console.WriteLine("Fehler: Der Token wurde nicht aus der .env-Datei geladen!");
                return;
            }

            Console.WriteLine("Starte Bot...");

            // Configure the Discord client
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessageReactions
            };

            var client = new DiscordSocketClient(config);
            Commands = new CommandService();

            // Create the DI container and register services
            Services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(Commands)
                .AddSingleton<InteractionHandler>()
                .BuildServiceProvider();

            var bot = new Bot(client);
            CommandHandler.ClearTradeSessions();
            // Retrieve CommandsModule from DI container and register events
            client.ReactionAdded += CommandHandler.HandleReactionAdded;
            client.UserLeft += CommandHandler.HandleUserLeft;
            client.SelectMenuExecuted += InteractionHandler.HandleSelectMenu;

            // Register commands and start the bot
            await CommandHandler.RegisterCommandsAsync(Services);
            await bot.StartAsync(botToken);
        }
    }
}
