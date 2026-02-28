using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.AdminCommands;

/// <summary>
///     Provides a command to lock a specific set, preventing further modifications.
/// </summary>
public class LockSetCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Locks the specified set if it is not already locked.
    /// </summary>
    /// <param name="setId">
    ///     The identifier of the set to lock.
    /// </param>
    [Command("lock")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task LockSetAsync(string setId)
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("The bot is currently inactive and not responding to commands.");
            return;
        }
        if (!CommandHandler.LockedSets.Contains(setId))
        {
            CommandHandler.LockedSets.Add(setId);
            await ReplyAsync($"Set {setId} has been locked.");
        }
        else
        {
            await ReplyAsync($"Set {setId} is already locked.");
        }
    }
}