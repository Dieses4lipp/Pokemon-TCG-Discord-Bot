using Discord;
using Discord.Commands;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands;

/// <summary>
///     Provides a command to pull a pack of 9 random Pokémon cards from a specified set.
/// </summary>
public class PullPackCommand : ModuleBase<SocketCommandContext>
{
    /// <summary>
    ///     Pulls a pack of 9 random cards from the specified set, considering the locked sets and limits.
    /// </summary>
    [Command("pullpack")]
    [DailyUsageLimit(10)]
    public async Task PullPackAsync(string setId)
    {
        if (!CommandHandler.BotActive)
        {
            await ReplyAsync("💤 Bot is currently inactive. Use '!turnon' to activate the bot.");
            return;
        }
        if (CommandHandler.LockedSets.Contains(setId) || setId == null)
        {
            await ReplyAsync($"🔒 Set {setId} is locked and/or cannot be pulled.");
            return;
        }
        try
        {
            var allCards = await CommandHandler.GetRandomCards(100, setId);
            if (allCards.Count == 0)
            {
                await ReplyAsync($"❌ No cards found for set: {setId}!");
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

            CommandHandler.ActiveSessions[message.Id] =
                new PackSession(message.Id, Context.User.Id, selectedCardList);

            await message.AddReactionAsync(new Emoji("◀️"));
            await message.AddReactionAsync(new Emoji("▶️"));
            await message.AddReactionAsync(new Emoji("💾"));
            var userCollection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
            userCollection.PacksPulled++;
            CommandHandler.PullCount++;
            await CardStorage.SaveUserCardsAsync(userCollection);
        }
        catch (Exception ex)
        {
            await ReplyAsync($"⚠️ An error occurred while retrieving the pack: {ex.Message}");
        }
    }
}