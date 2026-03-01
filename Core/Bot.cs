using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.Commands.SlashCommandHandlers;

namespace DiscordBot.Core;

/// <summary>
///     Represents the bot and handles its initialization and commands.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="Bot"/> class.
/// </remarks>
/// <param name="client">
///     The <see cref="DiscordSocketClient"/> instance used by the bot.
/// </param>
public class Bot(DiscordSocketClient client)
{
    private readonly DiscordSocketClient _client = client;

    public static async Task RegisterGuildCommands(SocketGuild guild)
    {
        var commandList = new List<ApplicationCommandProperties>();

        commandList.AddRange([
                SlashCommands.PullCommand().Build(),
                SlashCommands.InventoryCommand().Build(),
                SlashCommands.HelpCommand().Build(),
        ]);

        // This one call handles everything: Adds, Updates, and Deletes
        await guild.BulkOverwriteApplicationCommandAsync([.. commandList]);
    }

    /// <summary>
    ///     Refreshing slash commands for the test server by deleting all existing commands and
    ///     re-adding them for debugging purposes.
    /// </summary>
    /// <param name="guild">
    ///     The name of the test server
    /// </param>
    public static async Task SyncCommands(SocketGuild guild)
    {
        Console.WriteLine("Deleting all slash commands from test server");
        await guild.DeleteApplicationCommandsAsync();

        var guildCommand = SlashCommands.PullCommand().Build();
        var helpCommand = SlashCommands.HelpCommand().Build();
        var inventoryCommand = SlashCommands.InventoryCommand().Build();

        List<ApplicationCommandProperties> builtSlashCommands = [
            guildCommand,
            helpCommand,
            inventoryCommand
            ];
        await guild.BulkOverwriteApplicationCommandAsync([.. builtSlashCommands]);
        Console.WriteLine("Re-added slash commands to test server");
    }

    public static async Task HandleButtonPressAsync(SocketMessageComponent component)
    {
        await (component.Data.CustomId switch
        {
            "next_card" => PullReactionHandler.HandleMoveCardIndex(component, 1),
            "prev_card" => PullReactionHandler.HandleMoveCardIndex(component, -1),
            "save_card" => PullReactionHandler.HandleSaveCardAsync(component),
            "inv_next_card" => InventoryReactionHandler.HandleMoveCardIndex(component, 1),
            "inv_prev_card" => InventoryReactionHandler.HandleMoveCardIndex(component, -1),
            "inv_fav_card" => InventoryReactionHandler.HandleFavoriteCard(component),
            "inv_delete_card" => InventoryReactionHandler.HandleDeleteCard(component),
            _ => Task.CompletedTask,
        });
    }

    /// <summary>
    ///     Handles the execution of slash commands received from the Discord client.
    /// </summary>
    /// <param name="cmd">
    ///     The <see cref="SocketSlashCommand"/> representing the slash command that was executed by
    ///     a user.
    /// </param>
    public async Task HandleSlashCommandAsync(SocketSlashCommand cmd) // DO NOT MARK AS STATIC, OTHERWISE BREAKS THE EVENT SUBSCRIPTION
    {
        Console.WriteLine($"Received slash command: '{cmd.CommandName}'");
        try
        {
            switch (cmd.CommandName)
            {
                case "pull":
                    await PullCommandHandler.Handle(cmd);
                    break;

                case "help":
                    await new HelpCommandHandler().Handle(cmd);
                    break;

                case "inventory":
                    await InventoryCommandHandler.Handle(cmd);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling slash command: {ex.Message}");
            await cmd.RespondAsync("An error occurred while processing the command.");
        }
    }

    /// <summary>
    ///     Starts the bot and logs it in using the provided token.
    /// </summary>
    /// <param name="botToken">
    ///     The token used to log in to the bot account.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    public async Task StartAsync(string botToken)
    {
        // Subscribe to events
        _client.UserLeft += CommandHandler.HandleUserLeft;
        _client.SelectMenuExecuted += InteractionHandler.HandleSelectMenu;
        _client.Log += Log;
        _client.Ready += OnReady;
        _client.MessageReceived += HandleCommandAsync;
        _client.ReactionAdded += (cache, channel, reaction) =>
        {
            _ = Task.Run(async () => await CommandHandler.HandleReactionAdded(cache, channel, reaction));
            return Task.CompletedTask;
        };

        // Register slash commands for the test server for debugging purposes
        _client.Ready += async () =>
            await SyncCommands(_client.Guilds.FirstOrDefault(g => g.Name == "DiesesPhilipp's server")!);

        // Registers slash commands for any guild the bot joins
        _client.JoinedGuild += async (guild) =>
            await RegisterGuildCommands(guild);

        _client.SlashCommandExecuted += HandleSlashCommandAsync;
        _client.ButtonExecuted += async (component) =>
            await HandleButtonPressAsync(component);

        Console.WriteLine("Starting bot...");
        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();
    }

    /// <summary>
    ///     Handles incoming messages and executes commands when prefixed with '!' character.
    /// </summary>
    /// <param name="arg">
    ///     The incoming <see cref="SocketMessage"/> to handle.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    public async Task HandleCommandAsync(SocketMessage arg)
    {
        if (arg is not SocketUserMessage message) return;
        if (message.Author.IsBot) return;

        Console.WriteLine($"Received message: '{message.Content}'");

        int argPos = 0;
        if (message.HasCharPrefix('!', ref argPos))
        {
            _ = Task.Run(async () =>
            {
                var context = new SocketCommandContext(_client, message);
                var result = await Program.Commands.ExecuteAsync(context, argPos, Program.Services);

                if (!result.IsSuccess)
                {
                    Console.WriteLine($"Error: {result.ErrorReason}");
                    await context.Channel.SendMessageAsync($"❌ Error: {result.ErrorReason}");
                    return;
                }
                Console.WriteLine("Command handled successfully!");
            });
        }
        else
        {
            Console.WriteLine("Message did not have '!' prefix.");
        }
    }

    /// <summary>
    ///     Logs messages to the console.
    /// </summary>
    /// <param name="logMessage">
    ///     The log message to log.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    private Task Log(LogMessage logMessage)
    {
        Console.WriteLine(logMessage);
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Executes when the bot is ready and connected to Discord.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    private Task OnReady()
    {
        Console.WriteLine("Bot is online and ready!");
        return Task.CompletedTask;
    }
}