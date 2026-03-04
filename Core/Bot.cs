using Discord;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.LockSetCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.StatsCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOffCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOnCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.UnlockSetCommand;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.CancelTradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.ConfirmTradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.TradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.HelpCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.InventoryCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.ProfileCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.RestartCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.SetsCommand;
using Microsoft.VisualBasic;

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
                SlashCommandBuilders.PullCommand().Build(),
                SlashCommandBuilders.InventoryCommand().Build(),
                SlashCommandBuilders.HelpCommand().Build(),
                SlashCommandBuilders.ProfileCommand().Build(),
                SlashCommandBuilders.StatsCommand().Build(),
                SlashCommandBuilders.TurnOnCommand().Build(),
                SlashCommandBuilders.TurnOffCommand().Build(),
                SlashCommandBuilders.RestartCommand().Build(),
                SlashCommandBuilders.LockSetCommand().Build(),
                SlashCommandBuilders.UnlockSetCommand().Build(),
                SlashCommandBuilders.TradeCommand().Build(),
                SlashCommandBuilders.ConfirmTradeCommand().Build(),
                SlashCommandBuilders.CancelTradeCommand().Build(),
                SlashCommandBuilders.SetsCommand().Build(),
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
        // Be careful RATE LIMIT !!!
        var guildCommand = SlashCommandBuilders.PullCommand().Build();
        Console.WriteLine("Successfully built pull command");
        var helpCommand = SlashCommandBuilders.HelpCommand().Build();
        Console.WriteLine("Successfully built help command");
        var inventoryCommand = SlashCommandBuilders.InventoryCommand().Build();
        Console.WriteLine("Successfully built inventory command");
        var profileCommand = SlashCommandBuilders.ProfileCommand().Build();
        Console.WriteLine("Successfully built profile command");
        var statsCommand = SlashCommandBuilders.StatsCommand().Build();
        Console.WriteLine("Successfully built stats command");
        var restartCommand = SlashCommandBuilders.RestartCommand().Build();
        Console.WriteLine("Successfully build restart command");
        var turnOffCommand = SlashCommandBuilders.TurnOffCommand().Build();
        Console.WriteLine("Successfully build turnoff command");
        var turnOnCommand = SlashCommandBuilders.TurnOnCommand().Build();
        Console.WriteLine("Successfully build turnon command");
        var lockSetCommand = SlashCommandBuilders.LockSetCommand().Build();
        Console.WriteLine("Successfully build lockset command");
        var unlockSetCommand = SlashCommandBuilders.UnlockSetCommand().Build();
        Console.WriteLine("Successfully build unlockset command");
        var tradeCommand = SlashCommandBuilders.TradeCommand().Build();
        Console.WriteLine("Successfully build trade command");
        var confirmTradeCommand = SlashCommandBuilders.ConfirmTradeCommand().Build();
        Console.WriteLine("Successfully build confirmtrade command");
        var cancelTradeCommand = SlashCommandBuilders.CancelTradeCommand().Build();
        Console.WriteLine("Successfully build canceltrade command");
        var setsCommand = SlashCommandBuilders.SetsCommand().Build();
        Console.WriteLine("Successfully build sets command");
        List<ApplicationCommandProperties> builtSlashCommands = [
            guildCommand,
            helpCommand,
            inventoryCommand,
            profileCommand,
            statsCommand,
            restartCommand,
            turnOffCommand,
            turnOnCommand,
            lockSetCommand,
            unlockSetCommand,
            tradeCommand,
            confirmTradeCommand,
            cancelTradeCommand,
            setsCommand,
            ];
        Console.WriteLine("Adding slash commands to test server");
        await guild.BulkOverwriteApplicationCommandAsync([.. builtSlashCommands]);
        Console.WriteLine("Re-added slash commands to test server");
    }

    /// <summary>
    ///     Handles the button press event
    /// </summary>
    /// <param name="component">
    ///     The <see cref="SocketMessageComponent"/> which is pressed
    /// </param>
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
                    await new HelpCommandHandler().Handle(cmd, _client.CurrentUser.GetAvatarUrl());
                    break;

                case "inventory":
                    await InventoryCommandHandler.Handle(cmd);
                    break;

                case "profile":
                    await ProfileCommandHandler.Handle(cmd);
                    break;

                case "stats":
                    await StatsCommandHandler.Handle(cmd, _client);
                    break;

                case "restart":
                    await RestartCommandHandler.Handle(cmd);
                    break;

                case "turnoff":
                    await TurnOffCommandHandler.Handle(cmd);
                    break;

                case "turnon":
                    await TurnOnCommandHandler.Handle(cmd);
                    break;

                case "lockset":
                    await LockSetCommandHandler.Handle(cmd);
                    break;

                case "unlockset":
                    await UnlockSetCommandHandler.Handle(cmd);
                    break;

                case "trade":
                    await TradeCommandHandler.Handle(cmd);
                    break;

                case "confirmtrade":
                    await ConfirmTradeCommandHandler.Handle(cmd);
                    break;

                case "canceltrade":
                    await CancelTradeCommandHandler.Handle(cmd);
                    break;

                case "sets":
                    await TestSetsCommandHandler.Handle(cmd);
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
        _client.Log += Log;
        _client.Ready += OnReady;

        // Register slash commands for the test server for debugging purposes
        _client.Ready += async () =>
            await SyncCommands(_client.Guilds.FirstOrDefault(g => g.Name == "DiesesPhilipp's server")!);

        // Registers slash commands for any guild the bot joins
        _client.JoinedGuild += async (guild) =>
            await RegisterGuildCommands(guild);

        _client.AutocompleteExecuted += HandleAutoCompleteAsync;
        _client.SlashCommandExecuted += HandleSlashCommandAsync;
        _client.ButtonExecuted += async (component) =>
            await HandleButtonPressAsync(component);

        Console.WriteLine("Starting bot...");
        await _client.LoginAsync(TokenType.Bot, botToken);
        await _client.StartAsync();
    }

    public async Task HandleAutoCompleteAsync(SocketAutocompleteInteraction interaction)
    {
        switch (interaction.Data.CommandName)
        {
            case "pull":
                await PullAutocompleteHandler.Handle(interaction);
                break;
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