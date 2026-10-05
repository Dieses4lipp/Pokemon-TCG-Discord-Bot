using Discord;
using Discord.WebSocket;
using DiscordBot.Commands;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.LockSetCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.StatsCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOffCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOnCommand;
using DiscordBot.Commands.SlashCommandHandlers.AdminCommands.UnlockSetCommand;
using DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.CancelTradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.ConfirmTradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TradeCommands.TradeCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.HelpCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.InventoryCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.ProfileCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.RestartCommand;
using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.SetsCommand;

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

    private bool _commandsRegistered;

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
                SlashCommandBuilders.ExpeditionCommand().Build(),
        ]);

        // This one call handles everything: Adds, Updates, and Deletes
        await guild.BulkOverwriteApplicationCommandAsync([.. commandList]);
    }

    /// <summary>
    ///     Handles the button press event
    /// </summary>
    /// <param name="component">
    ///     The <see cref="SocketMessageComponent"/> which is pressed
    /// </param>
    public static async Task HandleButtonPressAsync(SocketMessageComponent component)
    {
        try
        {
            await DispatchButtonPressAsync(component);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling button '{component.Data.CustomId}': {ex}");
            await ReportErrorAsync(component, "An error occurred while processing this action.");
        }
    }

    /// <summary>
    ///     Routes a button press to its handler based on the custom ID
    /// </summary>
    /// <param name="component">
    ///     The <see cref="SocketMessageComponent"/> which is pressed
    /// </param>
    private static Task DispatchButtonPressAsync(SocketMessageComponent component)
    {
        return component.Data.CustomId switch
        {
            "open_pack" => PullReactionHandler.HandleOpenPackAsync(component),
            "next_card" => PullReactionHandler.HandleMoveCardIndex(component, 1),
            "prev_card" => PullReactionHandler.HandleMoveCardIndex(component, -1),
            "save_card" => PullReactionHandler.HandleSaveCardAsync(component),
            "sell_pack" => PullReactionHandler.HandleSellPackAsync(component),
            "inv_next_card" => InventoryReactionHandler.HandleMoveCardIndex(component, 1),
            "inv_prev_card" => InventoryReactionHandler.HandleMoveCardIndex(component, -1),
            "inv_fav_card" => InventoryReactionHandler.HandleFavoriteCard(component),
            "inv_sell_card" => InventoryReactionHandler.HandleSellCard(component),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>
    ///     Sends an error message to the user, using a followup if the interaction was already
    ///     deferred or responded to. Never throws, so it is safe to call from a catch block.
    /// </summary>
    /// <param name="interaction">
    ///     The interaction that failed
    /// </param>
    /// <param name="message">
    ///     The error message shown to the user
    /// </param>
    private static async Task ReportErrorAsync(SocketInteraction interaction, string message)
    {
        try
        {
            if (interaction.HasResponded)
                await interaction.FollowupAsync(message, ephemeral: true);
            else
                await interaction.RespondAsync(message, ephemeral: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to report error to user: {ex.Message}");
        }
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
                    await SetsCommandHandler.Handle(cmd);
                    break;

                case "expedition":
                    await ExpeditionCommandHandler.Handle(cmd);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling slash command '{cmd.CommandName}': {ex}");
            await ReportErrorAsync(cmd, "An error occurred while processing the command.");
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

        // Register slash commands for every guild once; Ready fires again on each reconnect
        _client.Ready += RegisterCommandsOnFirstReadyAsync;

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

            case "expedition":
                await ExpeditionAutocompleteHandler.Handle(interaction);
                break;
        }
    }

    /// <summary>
    ///     Registers slash commands for all guilds the bot is in, only on the first Ready event.
    /// </summary>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    private async Task RegisterCommandsOnFirstReadyAsync()
    {
        if (_commandsRegistered) return;
        _commandsRegistered = true;

        foreach (var guild in _client.Guilds)
        {
            try
            {
                await RegisterGuildCommands(guild);
                Console.WriteLine($"Registered slash commands for guild {guild.Name} ({guild.Id}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register slash commands for guild {guild.Name} ({guild.Id}): {ex}");
            }
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