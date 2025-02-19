using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot;

/// <summary>
///     Represents the bot and handles its initialization and commands.
/// </summary>
public class Bot
{
    private readonly DiscordSocketClient _client;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Bot" /> class.
    /// </summary>
    /// <param name="client">
    ///     The <see cref="DiscordSocketClient" /> instance used by
    ///     the bot.
    /// </param>
    public Bot(DiscordSocketClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Starts the bot and logs it in using the provided token.
    /// </summary>
    /// <param name="botToken">The token used to log in to the bot account.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task StartAsync(string botToken)
    {
        _client.Log += Log;
        _client.Ready += OnReady;
        _client.MessageReceived += HandleCommandAsync;

        Console.WriteLine("Starting bot...");
        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();
        await Task.Delay(-1);
    }

    /// <summary>
    ///     Logs messages to the console.
    /// </summary>
    /// <param name="logMessage">The log message to log.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task Log(LogMessage logMessage)
    {
        Console.WriteLine(logMessage);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Executes when the bot is ready and connected to Discord.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    private Task OnReady()
    {
        Console.WriteLine("Bot is online and ready!");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Handles incoming messages and executes commands when prefixed with '!'
    ///     character.
    /// </summary>
    /// <param name="arg">The incoming <see cref="SocketMessage" /> to handle.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                await context.Channel.SendMessageAsync($"❌ Error: {result.ErrorReason}");
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