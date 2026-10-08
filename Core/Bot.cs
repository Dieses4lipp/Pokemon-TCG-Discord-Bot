using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     Connects the Discord client to the interaction modules in <c>Modules/</c>.
/// </summary>
public sealed class Bot(
    DiscordSocketClient client,
    InteractionService interactions,
    IServiceProvider services,
    UserRepository users,
    ILogger<Bot> logger)
{
    private bool _commandsRegistered;

    /// <summary>
    ///     Loads the interaction modules, subscribes to client events and logs the bot in.
    /// </summary>
    /// <param name="botToken">
    ///     The token used to log in to the bot account.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    public async Task StartAsync(string botToken)
    {
        client.Log += Log;
        interactions.Log += Log;
        client.Ready += OnReady;

        // Register slash commands for every guild once; Ready fires again on each reconnect
        client.Ready += RegisterCommandsOnFirstReadyAsync;

        // Registers slash commands for any guild the bot joins
        client.JoinedGuild += RegisterGuildCommandsAsync;

        client.UserLeft += HandleUserLeft;
        client.InteractionCreated += HandleInteractionAsync;

        await interactions.AddModulesAsync(typeof(Bot).Assembly, services);

        logger.LogInformation("Logging in to Discord");
        await client.LoginAsync(TokenType.Bot, botToken);
        await client.StartAsync();
    }

    /// <summary>
    ///     Routes a slash command, button press or autocomplete request to its module and reports
    ///     failures to the user.
    /// </summary>
    /// <param name="interaction">
    ///     The interaction Discord sent.
    /// </param>
    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        IResult result;
        try
        {
            result = await interactions.ExecuteCommandAsync(new SocketInteractionContext(client, interaction), services);
        }
        catch (Exception ex)
        {
            result = ExecuteResult.FromError(ex);
        }

        if (result.IsSuccess || result.Error == InteractionCommandError.UnknownCommand)
            return;

        if (result.Error == InteractionCommandError.UnmetPrecondition)
        {
            await ReportErrorAsync(interaction, result.ErrorReason);
            return;
        }

        if (result is ExecuteResult { Exception: { } exception })
            logger.LogError(exception, "Error handling interaction '{Interaction}'", DescribeInteraction(interaction));
        else
            logger.LogError("Error handling interaction '{Interaction}': {Reason}", DescribeInteraction(interaction), result.ErrorReason);

        // Autocomplete requests cannot carry a message
        if (interaction is not SocketAutocompleteInteraction)
            await ReportErrorAsync(interaction, "An error occurred while processing this action.");
    }

    private static string DescribeInteraction(SocketInteraction interaction) => interaction switch
    {
        SocketSlashCommand command => command.CommandName,
        SocketMessageComponent component => component.Data.CustomId,
        SocketAutocompleteInteraction autocomplete => $"{autocomplete.Data.CommandName} (autocomplete)",
        _ => interaction.Type.ToString(),
    };

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
    private async Task ReportErrorAsync(SocketInteraction interaction, string message)
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
            logger.LogWarning(ex, "Failed to report error to user");
        }
    }

    /// <summary>
    ///     Replaces the guild's slash commands with the ones defined by the modules.
    /// </summary>
    /// <param name="guild">
    ///     The guild to register the commands in.
    /// </param>
    private async Task RegisterGuildCommandsAsync(SocketGuild guild)
    {
        await interactions.RegisterCommandsToGuildAsync(guild.Id, deleteMissing: true);
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

        foreach (var guild in client.Guilds)
        {
            try
            {
                await RegisterGuildCommandsAsync(guild);
                logger.LogInformation("Registered slash commands for guild {GuildName} ({GuildId})", guild.Name, guild.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to register slash commands for guild {GuildName} ({GuildId})", guild.Name, guild.Id);
            }
        }
    }

    /// <summary>
    ///     Handles cleanup when a user leaves a guild by archiving their saved card collection.
    ///     The collection is restored on the user's next command, so leaving one guild while still
    ///     playing in another (or rejoining) does not lose any data.
    /// </summary>
    /// <param name="user">
    ///     The user who left.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    private async Task HandleUserLeft(SocketGuild _, SocketUser user)
    {
        if (await users.ArchiveUserCardsAsync(user.Id))
        {
            logger.LogInformation("Archived collection of user {Username} ({UserId})", user.Username, user.Id);
        }
        else
        {
            logger.LogInformation("No collection found for user {Username} ({UserId})", user.Username, user.Id);
        }
    }

    /// <summary>
    ///     Forwards Discord.Net log messages to the logger.
    /// </summary>
    /// <param name="logMessage">
    ///     The log message to log.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    private Task Log(LogMessage logMessage)
    {
        var level = logMessage.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            _ => LogLevel.Trace,
        };
        logger.Log(level, logMessage.Exception, "{Source}: {Message}", logMessage.Source, logMessage.Message);
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
        logger.LogInformation("Bot is online and ready");
        return Task.CompletedTask;
    }
}
