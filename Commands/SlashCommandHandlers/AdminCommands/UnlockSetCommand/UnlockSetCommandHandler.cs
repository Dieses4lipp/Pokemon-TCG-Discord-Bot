using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.AdminCommands.UnlockSetCommand;

/// <summary>
///     A class containing the Handler for the /unlockset command
/// </summary>
public static class UnlockSetCommandHandler
{
    /// <summary>
    ///     Handles the /unlockset command, locking a specific set
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

        var setId = command.Data.Options.First().Value as string;

        if (string.IsNullOrWhiteSpace(setId))
        {
            await command.FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        if (CommandHandler.LockedSets.Remove(setId))
        {
            await command.FollowupAsync($"🔓 **Set Unlocked:** Users can now pull from `{setId}` again.");
        }
        else
        {
            await command.FollowupAsync($"ℹ️ Set `{setId}` wasn't locked to begin with.");
        }
    }
}