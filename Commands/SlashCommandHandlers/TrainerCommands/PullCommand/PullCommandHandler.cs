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

        var langOption = command.Data.Options.FirstOrDefault(o => o.Name == "language");

        var lang = "en";
        if (langOption != null)
            lang = langOption.Value as string;

        // Safety check for null or locked sets
        if (string.IsNullOrWhiteSpace(setId) || CommandHandler.LockedSets.Contains(setId))
        {
            await command.FollowupAsync($"🔒 Set `{setId ?? "Unknown"}` is locked or unavailable.", ephemeral: true);
            return;
        }

        try
        {
            // Fetch cards (Cache these in a real production environment to save API hits!)
            var allCards = await CommandHandler.GetRandomCards(250, setId, lang!);

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

            var projectRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\");
            var setFolder = Path.Combine(projectRoot, $@"Assets\sets_covers\{setId}");

            string packImagePath;

            if (Directory.Exists(setFolder))
            {
                var coverImages = Directory.GetFiles(setFolder, "*.jpg").ToList();


                if (coverImages.Count > 0)
                {
                    //pick a random cover image from the set folder
                    packImagePath = coverImages[random.Next(coverImages.Count)];
                }
                else
                {
                    // Fallback to default.jpg
                    packImagePath = Path.Combine(projectRoot, @"Assets\sets_covers\default.jpg");
                }
            }
            else
            {
                packImagePath = Path.Combine(projectRoot, @"Assets\sets_covers\default.jpg");
            }

            if (!File.Exists(packImagePath))
            {
                await command.FollowupAsync($"❌ Pack image not found for set: {setId}", ephemeral: true);
                return;
            }

            const double packCost = 5.0;
            var userCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);

            if (userCollection.Balance < packCost)
            {
                await command.FollowupAsync(
                    $"❌ **Insufficient balance!**\n\n" +
                    $"Pack cost: **{packCost:F2} EUR**\n" +
                    $"Your balance: **{userCollection.Balance:F2} EUR**\n" +
                    $"Missing: **{(packCost - userCollection.Balance):F2} EUR**\n\n" +
                    $"💡 Sell some cards to earn more credits!",
                    ephemeral: true);
                return;
            }

            var packEmbed = new EmbedBuilder()
                .WithTitle($"")
                .WithImageUrl($"attachment://{Path.GetFileName(packImagePath)}")
                .WithColor(Color.Blue)
                .Build();

            var openPackButton = new ComponentBuilder()
                .WithButton("Open Pack", "open_pack", ButtonStyle.Primary, new Emoji("🎁"))
                .Build();

            var response = await command.FollowupWithFileAsync(
                packImagePath,
                embed: packEmbed,
                ephemeral: true,
                components: openPackButton
            );

            // Store pack session with cards ready to be revealed
            PullReactionHandler.ActiveSessions[response.Id] = new PackSession(response.Id, command.User.Id, selectedCardList);

            // Update Stats
            userCollection.PacksPulled++;
            CommandHandler.PullCount++;
            // Deduct pack cost
            userCollection.Balance -= packCost;
            await CardStorage.SaveUserCardsAsync(userCollection);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pull Error: {ex.Message}");
            await command.FollowupAsync("⚠️ System error while opening pack.", ephemeral: true);
        }
    }
}