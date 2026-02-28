using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.AdminCommands;

/// <summary>
///     Provides a command to restart the bot.
/// </summary>
public class RestartCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Restarts the bot if it is currently active.
    /// </summary>
    [Command("restart")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task RestartAsync()
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("💤 Bot is currently inactive. Use '!turnon' to activate the bot.");
            return;
        }
        await ReplyAsync("🔄 Restarting...");
        Program.RestartBot();
        await ReplyAsync("✅ Bot has been restarted.");
    }
}