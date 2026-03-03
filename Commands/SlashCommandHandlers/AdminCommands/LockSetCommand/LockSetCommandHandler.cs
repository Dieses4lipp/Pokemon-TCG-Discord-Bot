using Discord.WebSocket;
using DiscordBot.Core;

namespace DiscordBot.Commands.SlashCommandHandlers.AdminCommands.LockSetCommand;

/// <summary>
///     A class that contains the Handler for the /lockset command
/// </summary>
public static class LockSetCommandHandler
{
    /// <summary>
    ///     Handles a slash command to lock a specified set, restricting user access to it.
    /// </summary>
    /// <param name="command">
    ///     The slash command that contains the user and options for locking a set.
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

        var setId = command.Data.Options.FirstOrDefault(c => c.Name == "set-id")?.Value as string;

        if (string.IsNullOrWhiteSpace(setId))
        {
            await command.FollowupAsync("❌ Invalid Set ID.");
            return;
        }

        bool wasAdded = CommandHandler.LockedSets.Add(setId);

        if (wasAdded)
        {
            await command.FollowupAsync($"🔒 **Set Locked:** Users can no longer pull from `{setId}`.");
        }
        else
        {
            await command.FollowupAsync($"⚠️ Set `{setId}` is already in the locked list.");
        }
    }
}