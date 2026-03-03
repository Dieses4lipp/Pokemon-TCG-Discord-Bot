using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.AdminCommands.TurnOnCommand;

/// <summary>
///     A class containing the Handler for the /turnon command
/// </summary>
public static class TurnOnCommandHandler
{
    /// <summary>
    ///     Handles the /turnon command
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
            await command.FollowupAsync("❌ Only the bot owner can turn on the bot.");
            return;
        }

        CommandHandler.BotActive = true;

        await command.FollowupAsync("🔌 Bot is now active.");
    }
}