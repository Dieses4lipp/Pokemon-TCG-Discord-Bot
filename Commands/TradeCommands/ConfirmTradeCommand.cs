﻿using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands.TradeCommands
{
    public class ConfirmTradeCommand : ModuleBase<SocketCommandContext>
    {
        [Command("confirmtrade")]
        public async Task ConfirmTradeAsync()
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }

            if (!CommandHandler.ActiveTrades.ContainsKey(Context.User.Id))
            {
                await ReplyAsync("You don't have any pending trades.");
                return;
            }

            var tradeSession = CommandHandler.ActiveTrades[Context.User.Id];
            if (tradeSession.ReceiverId != Context.User.Id)
            {
                await ReplyAsync("You are not the receiver of this trade.");
                return;
            }

            var senderCollection = await CardStorage.LoadUserCardsAsync(tradeSession.SenderId);
            var receiverCollection = await CardStorage.LoadUserCardsAsync(tradeSession.ReceiverId);

            var cardToRemove = senderCollection.Cards.FirstOrDefault(c => c.Name == tradeSession.CardToTrade.Name);
            if (cardToRemove != null)
            {
                senderCollection.Cards.Remove(cardToRemove);
            }
            receiverCollection.Cards.Add(tradeSession.CardToTrade);

            senderCollection.CardsTraded++;
            receiverCollection.CardsTraded++;

            await CardStorage.SaveUserCardsAsync(senderCollection);
            await CardStorage.SaveUserCardsAsync(receiverCollection);

            CommandHandler.ActiveTrades.Remove(tradeSession.SenderId);
            CommandHandler.ActiveTrades.Remove(tradeSession.ReceiverId);

            await ReplyAsync("Trade completed successfully!");
        }
    }
}
