using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers;

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
        // Defer the response to give more time to process the command
        await command.DeferAsync(ephemeral: true);

        var setId = command.Data.Options.FirstOrDefault(o => o.Name == "set-id")?.Value as string;

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is currently inactive. Use '/turnon' to activate the bot.", ephemeral: true);
            return;
        }
        if (setId == null || CommandHandler.LockedSets.Contains(setId))
        {
            await command.FollowupAsync($"🔒 Set {setId} is locked and/or cannot be pulled.", ephemeral: true);
            return;
        }
        try
        {
            var allCards = await CommandHandler.GetRandomCards(100, setId);

            if (allCards.Count == 0)
            {
                await command.FollowupAsync($"❌ No cards found for set: {setId}!", ephemeral: true);
                await command.FollowupAsync($"❌ No cards found for set: {setId}!", ephemeral: true);
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

            var buttons = new ComponentBuilder()
                .WithButton("Previous", "prev_card", ButtonStyle.Secondary)
                .WithButton("Next", "next_card", ButtonStyle.Secondary)
                .WithButton("Save", "save_card", ButtonStyle.Success)
                .Build();

            var response = await command.FollowupAsync(ephemeral: true, components: buttons, embed: embed);

            PullReactionHandler.ActiveSessions[response.Id] =
                new PackSession(response.Id, command.User.Id, selectedCardList);

            var userCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);

            userCollection.PacksPulled++;

            CommandHandler.PullCount++;

            await CardStorage.SaveUserCardsAsync(userCollection);
        }
        catch (Exception ex)
        {
            await command.FollowupAsync($"⚠️ An error occurred while retrieving the pack: {ex.Message}", ephemeral: true);
        }

        Console.WriteLine("Handled /pull command successfully.");
    }
}