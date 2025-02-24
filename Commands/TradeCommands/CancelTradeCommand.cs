using Discord.Commands;
using DiscordBot.Core;
using System.Threading.Tasks;

namespace DiscordBot.Commands.TradeCommands
{
    public class CancelTradeCommand : ModuleBase<SocketCommandContext>
    {
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
}
