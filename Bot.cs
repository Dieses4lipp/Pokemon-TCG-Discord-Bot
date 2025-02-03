using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DiscordBot
{
    public class Bot
    {
        private readonly DiscordSocketClient _client;

        public Bot(DiscordSocketClient client)
        {
            _client = client;
        }

        public async Task StartAsync(string botToken)
        {
            _client.Log += Log;
            _client.Ready += OnReady;
            _client.MessageReceived += HandleCommandAsync;

            Console.WriteLine("Starte Bot...");
            await _client.LoginAsync(TokenType.Bot, botToken);
            await _client.StartAsync();
            await Task.Delay(-1);
        }

        private Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage);
            return Task.CompletedTask;
        }

        private Task OnReady()
        {
            Console.WriteLine("Bot ist online und bereit!");
            return Task.CompletedTask;
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            if (arg is not SocketUserMessage message) return;
            if (message.Author.IsBot) return;

            Console.WriteLine($"Received message: '{message.Content}'");

            int argPos = 0;
            if (message.HasCharPrefix('!', ref argPos))
            {
                var context = new SocketCommandContext(_client, message);
                var result = await Program.Commands.ExecuteAsync(context, argPos, Program.Services);

                if (!result.IsSuccess)
                {
                    Console.WriteLine($"Error: {result.ErrorReason}");
                }
                else
                {
                    Console.WriteLine($"Command executed successfully: {message.Content}");
                }
            }
            else
            {
                Console.WriteLine("Message did not have '!' prefix.");
            }
        }

    }
}
