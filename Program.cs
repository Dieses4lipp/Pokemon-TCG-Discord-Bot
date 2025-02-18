using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace DiscordBot
{
    class Program
    {
        public static CommandService Commands { get; set; }
        public static IServiceProvider Services { get; private set; }

        private CommandsModule _commandsModule;
        static async Task Main(string[] args)
        {
            await new Program().RunBotAsync();

        }
        public async Task RunBotAsync()
        {
            Env.Load();
            string botToken = Environment.GetEnvironmentVariable("TOKEN");
            if (string.IsNullOrEmpty(botToken))
            {
                Console.WriteLine("Fehler: Der Token wurde nicht aus der .env-Datei geladen!");
            }

            Console.WriteLine("Starte Bot...");

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                 GatewayIntents.GuildMessages |
                                 GatewayIntents.MessageContent |
                                 GatewayIntents.GuildMessageReactions
            };

            var client = new DiscordSocketClient(config);
            Commands = new CommandService();

            // Erstelle den DI-Container und registriere die Services
            Services = new ServiceCollection()
    .AddSingleton(client)
    .AddSingleton(Commands)
    .AddSingleton<CommandsModule>()
    .AddSingleton<InteractionHandler>() 
    .BuildServiceProvider();

            var bot = new Bot(client);

            // Hole CommandsModule aus DI und registriere das Event
            var commandsModule = Services.GetRequiredService<CommandsModule>();
            client.ReactionAdded += commandsModule.HandleReactionAdded;
            client.ReactionAdded += commandsModule.HandleSetReactionAdded;
            client.UserLeft += commandsModule.HandleUserLeft;
            var interactionHandler = Services.GetRequiredService<InteractionHandler>();
            client.SelectMenuExecuted += interactionHandler.HandleSelectMenu;



            await CommandHandler.RegisterCommandsAsync(Services);
            await bot.StartAsync(botToken);
        }

    }
}
