using Discord.Commands;
using DiscordBot.Core;

namespace DiscordBot.Commands.TradeCommands;

/// <summary>
///     Provides a command to cancel an active trade session.
/// </summary>
public class CancelTradeCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Cancels the trade if the user has an active trade session.
    /// </summary>
    [Command("canceltrade")]
    public async Task CancelTradeAsync()
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("The bot is currently inactive and not responding to commands.");
            return;
        }

        if (!CommandHandler.ActiveTrades.ContainsKey(Context.User.Id))
        {
            await ReplyAsync("You don't have any pending trades to cancel.");
            return;
        }

        var tradeSession = CommandHandler.ActiveTrades[Context.User.Id];
        CommandHandler.ActiveTrades.Remove(tradeSession.SenderId);
        CommandHandler.ActiveTrades.Remove(tradeSession.ReceiverId);

        await ReplyAsync("Trade has been canceled.");
    }
}