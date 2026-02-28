using Discord;
using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.AdminCommands;

/// <summary>
///     Provides a command to turn on the bot.
/// </summary>
public class TurnOnCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Turns on the bot, making it active.
    /// </summary>
    [Command("turnon")]
    [RequireUserPermission(GuildPermission.Administrator)]
    public async Task TurnOnAsync()
    {
        CommandHandler.BotActive = true;
        await ReplyAsync("🔌 Bot is now active.");
    }
}