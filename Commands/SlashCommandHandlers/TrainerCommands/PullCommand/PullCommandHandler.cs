using Discord;
using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;

namespace DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;

/// <summary>
///     A class that handles the /pull slash command, allowing users to pull a booster pack from a
///     specified set. Pack size, rarity odds and price are configured per set in Data/packSettings.json.
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
            // Pack layout (card count, slot odds, price) and covers come from Data/packSettings.json
            PackProfile profile = PackSettingsProvider.GetProfile(setId);
            double packCost = profile.PackPrice;

            // Check the balance before loading any cards, so broke users cost no API calls
            if (!await HasEnoughBalanceAsync(command, packCost))
                return;

            var allCards = await SetCardCache.GetSetCardsAsync(setId, lang!);

            if (allCards.Count == 0)
            {
                await command.FollowupAsync($"❌ No cards found for set: {setId}!", ephemeral: true);
                return;
            }

            var random = new Random();

            var uncoveredRarities = PackBuilder.GetUncoveredRarities(profile, allCards);
            if (uncoveredRarities.Count > 0)
                Console.WriteLine($"Pack settings: set '{setId}' has rarities no slot can roll: {string.Join(", ", uncoveredRarities)}");

            var selectedCardList = PackBuilder.BuildPack(profile, allCards, random);

            string packImagePath = PackSettingsProvider.GetRandomCoverPath(setId, random);

            if (!File.Exists(packImagePath))
            {
                await command.FollowupAsync($"❌ Pack image not found for set: {setId}", ephemeral: true);
                return;
            }

            // Reload: loading the set can take a while and the balance may have changed meanwhile
            var userCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);
            if (userCollection.Balance < packCost)
            {
                await SendInsufficientBalanceAsync(command, packCost, userCollection.Balance);
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
            await BotStateStore.SaveAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Pull Error: {ex}");
            await command.FollowupAsync("⚠️ System error while opening pack.", ephemeral: true);
        }
    }

    /// <summary>
    ///     Checks whether the user can afford a pack and tells them if not.
    /// </summary>
    /// <param name="command">
    ///     The /pull command interaction.
    /// </param>
    /// <param name="packCost">
    ///     The price of the pack.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if the user's balance covers the pack.
    /// </returns>
    private static async Task<bool> HasEnoughBalanceAsync(SocketSlashCommand command, double packCost)
    {
        var userCollection = await CardStorage.LoadUserCardsAsync(command.User.Id);
        if (userCollection.Balance >= packCost)
            return true;

        await SendInsufficientBalanceAsync(command, packCost, userCollection.Balance);
        return false;
    }

    /// <summary>
    ///     Tells the user their balance is too low for the pack.
    /// </summary>
    /// <param name="command">
    ///     The /pull command interaction.
    /// </param>
    /// <param name="packCost">
    ///     The price of the pack.
    /// </param>
    /// <param name="balance">
    ///     The user's current balance.
    /// </param>
    private static Task SendInsufficientBalanceAsync(SocketSlashCommand command, double packCost, double balance)
    {
        return command.FollowupAsync(
            $"❌ **Insufficient balance!**\n\n" +
            $"Pack cost: **{packCost:F2} EUR**\n" +
            $"Your balance: **{balance:F2} EUR**\n" +
            $"Missing: **{(packCost - balance):F2} EUR**\n\n" +
            $"💡 Sell some cards to earn more credits!",
            ephemeral: true);
    }
}
