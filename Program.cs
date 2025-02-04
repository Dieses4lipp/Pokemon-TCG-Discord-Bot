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
            _commandsModule = new CommandsModule();
            client.ReactionAdded += _commandsModule.HandleReactionAdded;
            Commands = new CommandService();
            Services = new ServiceCollection().AddSingleton(client).AddSingleton(Commands).BuildServiceProvider();
            var bot = new Bot(client);
            await CommandHandler.RegisterCommandsAsync(Program.Services);

            await bot.StartAsync(botToken);
        }
    }
}
