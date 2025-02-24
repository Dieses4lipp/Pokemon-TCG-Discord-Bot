using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Threading.Tasks;

namespace DiscordBot.Commands.TradeCommands
{
    public class TradeCommand : ModuleBase<SocketCommandContext>
    {
        [Command("trade")]
        public async Task TradeAsync(SocketUser user, int cardIndex)
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            var guildUser = Context.Guild.GetUser(user.Id);
            if (guildUser == null)
            {
                await ReplyAsync("User not found in this guild. Please mention a valid user.");
                return;
            }

            if (CommandHandler.ActiveTrades.ContainsKey(Context.User.Id) || CommandHandler.ActiveTrades.ContainsKey(user.Id))
            {
                await ReplyAsync("A trade session is already active for one of the users. Please complete or cancel the existing trade first.");
                return;
            }
            var senderCollection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
            var receiverCollection = await CardStorage.LoadUserCardsAsync(user.Id);

            if (cardIndex < 1 || cardIndex > senderCollection.Cards.Count)
            {
                await ReplyAsync("Invalid card index.");
                return;
            }

            var cardToTrade = senderCollection.Cards[cardIndex - 1];

            var tradeSession = new TradeSession(Context.User.Id, user.Id, cardToTrade);
            CommandHandler.ActiveTrades[Context.User.Id] = tradeSession;
            CommandHandler.ActiveTrades[user.Id] = tradeSession;
            CommandHandler.LogActiveTrades();
            await ReplyAsync($"{user.Mention}, {Context.User.Username} wants to trade {cardToTrade.Name} with you. Type !confirmtrade to accept.");
        }
    }
}
