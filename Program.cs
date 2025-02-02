using Discord;
using Discord.WebSocket;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using DiscordBot;
using DotNetEnv;

class Program
{
    private DiscordSocketClient _client;
    private CommandService _commands;
    private IServiceProvider _services;

    static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

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
                             GatewayIntents.MessageContent
        };
        _client = new DiscordSocketClient(config);
        _commands = new CommandService();
        _services = new ServiceCollection().AddSingleton(_client).AddSingleton(_commands).BuildServiceProvider();

        _client.Log += Log;
        _client.Ready += OnReady;
        _client.MessageReceived += HandleCommandAsync;
        await RegisterCommandsAsync();
        Console.WriteLine("RegisterCommandsAsync() wurde aufgerufen!");
        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();

        await Task.Delay(-1);
    }



    private Task Log(LogMessage arg)
    {
        Console.WriteLine(arg);
        return Task.CompletedTask;
    }

    private Task OnReady()
    {
        Console.WriteLine("Bot ist online und bereit!");
        return Task.CompletedTask;
    }

    public async Task RegisterCommandsAsync()
    {
        Console.WriteLine("Registriere Commands...");

        _commands = new CommandService();
        _services = new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton(_commands)
            .BuildServiceProvider();

        await _commands.AddModulesAsync(typeof(CommandsModule).Assembly, _services);
        foreach (var command in _commands.Commands)
        {
            Console.WriteLine($"Geladener Befehl: {command.Name}");
        }

        Console.WriteLine("Commands erfolgreich registriert!");
    }

    private async Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return;
        if (message.Author.IsBot) return;

        Console.WriteLine($"Empfangene Nachricht: '{message.Content}'");

        int argPos = 0;
        if (message.HasCharPrefix('!', ref argPos))
        {
            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);

            if (!result.IsSuccess)
                Console.WriteLine($"Fehler: {result.ErrorReason}");
            else
                Console.WriteLine($"Befehl erfolgreich ausgeführt: {message.Content}");
        }
        else
        {
            Console.WriteLine("Nachricht hatte kein '!' als Präfix.");
        }
    }


}
