using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOffCommand;

/// <summary>
///     A class containing the Handler for the /turnoff command
/// </summary>
public static class TurnOffCommandHandler
{
    /// <summary>
    ///     Handles the /turnoff command, disabling the bot
    /// </summary>
    /// <param name="command">
    ///     The SocketSlashCommand object representing the command interaction.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (command.User.Id != 497422114056306709)
        {
            await command.FollowupAsync("❌ Only the bot owner can turn off the bot.");
            return;
        }

        CommandHandler.BotActive = false;

        await command.FollowupAsync("💤 Bot is now inactive.");
    }
}