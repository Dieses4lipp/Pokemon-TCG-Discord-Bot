using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DiscordBot.Commands
{
    public class PullPackCommand : ModuleBase<SocketCommandContext>
    {
        [Command("pullpack")]
        public async Task PullPackAsync(string setId)
        {
            if (!CommandHandler.BotActive)
            {
                await ReplyAsync("The bot is currently inactive and not responding to commands.");
                return;
            }
            if (setId != null && CommandHandler.LockedSets.Contains(setId))
            {
                await ReplyAsync($"Set {setId} is locked and cannot be pulled.");
                return;
            }
            try
            {
                var allCards = await CommandHandler.GetRandomCards(100, setId);
                if (allCards.Count == 0)
                {
                    await ReplyAsync($"No cards found for set: {setId}!");
                    return;
                }

                var random = new Random();
                var selectedCards = new HashSet<Card>();
                while (selectedCards.Count < 9)
                {
                    string selectedRarity = CommandHandler.RollRarity(random);
                    var possibleCards = allCards.Where(c => c.Rarity == selectedRarity).ToList();

                    var cardToAdd = possibleCards.Count > 0
                        ? possibleCards[random.Next(possibleCards.Count)]
                        : allCards[random.Next(allCards.Count)];

                    selectedCards.Add(cardToAdd);
                }

                var selectedCardList = selectedCards.ToList();
                var embed = CommandHandler.BuildCardEmbed(selectedCardList[0], 1, selectedCardList.Count);
                var message = await ReplyAsync(embed: embed);

                var session = new PackSession(message.Id, Context.User.Id, selectedCardList);
                CommandHandler.ActiveSessions[message.Id] = session;

                await message.AddReactionAsync(new Emoji("◀️"));
                await message.AddReactionAsync(new Emoji("▶️"));
                await message.AddReactionAsync(new Emoji("💾"));
                var userCollection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
                userCollection.PacksPulled++;
                await CardStorage.SaveUserCardsAsync(userCollection);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"An error occurred while retrieving the pack: {ex.Message}");
            }
        }
    }
}