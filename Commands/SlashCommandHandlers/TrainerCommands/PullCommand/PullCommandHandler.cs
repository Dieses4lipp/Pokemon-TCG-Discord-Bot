using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;

/// <summary>
///     A class that handles the /pull slash command, allowing users to pull a pack of 9 random
///     Pokémon cards from a specified set.
/// </summary>
public static class PullCommandHandler
{
    /// <summary>
    ///     Handles the /pull command.
    /// </summary>
    /// <param name="command">
    ///     The SocketSlashCommand object representing the command interaction.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive.", ephemeral: true);
            return;
        }

        var setId = command.Data.Options.FirstOrDefault(o => o.Name == "set-id")?.Value as string;

        // Safety check for null or locked sets
        if (string.IsNullOrWhiteSpace(setId) || CommandHandler.LockedSets.Contains(setId))
        {
            await command.FollowupAsync($"🔒 Set `{setId ?? "Unknown"}` is locked or unavailable.", ephemeral: true);
            return;
        }

        try
        {
            // Fetch cards (Cache these in a real production environment to save API hits!)
            var allCards = await CommandHandler.GetRandomCards(250, setId);

            if (allCards == null || allCards.Count == 0)
            {
                await command.FollowupAsync($"❌ No cards found for set: {setId}!", ephemeral: true);
                return;
            }

            var random = new Random();
            var selectedCardList = new List<Card>();

            // Optimized Gacha Loop
            for (int i = 0; i < 9; i++)
            {
                string selectedRarity = CommandHandler.RollRarity(random);

                // Filter cards by rarity
                var possibleCards = allCards.Where(c => c.Rarity == selectedRarity).ToList();

                // Fallback: If no cards exist for that specific rarity in this set, grab any card
                var cardToAdd = possibleCards.Count != 0
                    ? possibleCards[random.Next(possibleCards.Count)]
                    : allCards[random.Next(allCards.Count)];

                selectedCardList.Add(cardToAdd);
            }

            var embed = CommandHandler.BuildCardEmbed(selectedCardList[0], 1, selectedCardList.Count);

            var buttons = new ComponentBuilder()
                .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "next_card", ButtonStyle.Secondary)
                .WithButton("💾 Save Card", "save_card", ButtonStyle.Success)
                .Build();

            var response = await command.FollowupAsync(ephemeral: true, components: buttons, embed: embed);

            // Session management
            PullReactionHandler.ActiveSessions[response.Id] = new PackSession(response.Id, command.User.Id, selectedCardList);

            // Update Stats
            var userCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);
            userCollection.PacksPulled++;
            CommandHandler.PullCount++;
            await CardStorage.SaveUserCardsAsync(userCollection);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pull Error: {ex.Message}");
            await command.FollowupAsync("⚠️ System error while opening pack.", ephemeral: true);
        }
    }
}