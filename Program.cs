using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace DiscordBot
{
    /// <summary>
    /// The main entry point for the Discord bot, handles bot startup, command registration, and event subscriptions.
    /// </summary>
    class Program
    {
        public static CommandService Commands { get; set; }
        public static IServiceProvider Services { get; private set; }

        private CommandsModule _commandsModule;

        /// <summary>
        /// The main method that starts the bot asynchronously.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static async Task Main(string[] args)
        {
            await new Program().RunBotAsync();
        }

        /// <summary>
        /// Starts the bot, loads the environment variables, sets up the bot client, and registers commands.
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
                .AddSingleton<CommandsModule>()
                .AddSingleton<InteractionHandler>()
                .BuildServiceProvider();

            var bot = new Bot(client);

            // Retrieve CommandsModule from DI container and register events
            var commandsModule = Services.GetRequiredService<CommandsModule>();
            client.ReactionAdded += commandsModule.HandleReactionAdded;
            client.ReactionAdded += commandsModule.HandleSetReactionAdded;
            client.UserLeft += commandsModule.HandleUserLeft;

            var interactionHandler = Services.GetRequiredService<InteractionHandler>();
            client.SelectMenuExecuted += interactionHandler.HandleSelectMenu;

            // Register commands and start the bot
            await CommandHandler.RegisterCommandsAsync(Services);
            await bot.StartAsync(botToken);
        }
    }
}
