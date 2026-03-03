using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.RestartCommand;

/// <summary>
///     A class containing the Handler for the /restart command
/// </summary>
public static class RestartCommandHandler
{
    /// <summary>
    ///     Handles the /restart command
    /// </summary>
    /// <param name="command">
    ///     The SocketSlashCommand object representing the command interaction.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process the command
        await command.DeferAsync(ephemeral: true);
        // Enter Admin ID
        if (command.User.Id != 497422114056306709)
        {
            await command.FollowupAsync("❌ Only the bot owner can restart the bot.");
            return;
        }

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive.");
            return;
        }

        await command.FollowupAsync("🔄 Restarting...");

        await Task.Delay(1000);
        Program.RestartBot();
    }
}