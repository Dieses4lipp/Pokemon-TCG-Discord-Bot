using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.AdminCommands;

/// <summary>
///     Provides a command to turn off the bot.
/// </summary>
public class TurnOffCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Turns off the bot, making it inactive.
    /// </summary>
    [Command("turnoff")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task TurnOffAsync()
    {
        CommandHandler.BotActive = false;
        await ReplyAsync("💤 Bot is now inactive.");
    }
}