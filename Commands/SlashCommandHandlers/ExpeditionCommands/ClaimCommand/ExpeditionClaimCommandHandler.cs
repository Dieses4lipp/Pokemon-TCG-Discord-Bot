using Discord.WebSocket;
using DiscordBot.Core;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Commands.SlashCommandHandlers.ExpeditionCommands.ClaimCommand;

/// <summary>
///     A class containing the Handler for the /expedition claim subcommand.
/// </summary>
public static class ExpeditionClaimCommandHandler
{
    private static readonly Random _random = new();

    /// <summary>
    ///     Handles the /expedition claim subcommand, validating that an expedition exists and has
    ///     actually finished before rewards can be granted.
    /// </summary>
    /// <param name="command">
    ///     The slash command issued by the user, containing information about the command and the
    ///     user invoking it.
    /// </param>
    public static async Task Handle(SocketSlashCommand command)
    {
        // Defer the response to give more time to process
        await command.DeferAsync(ephemeral: true);

        if (!CommandHandler.BotActive)
        {
            await command.FollowupAsync("💤 Bot is inactive.", ephemeral: true);
            return;
        }

        var collection = await CardStorage.LoadUserCardsAsync(command.User.Id);
        var expedition = collection.ActiveExpedition;

        if (expedition == null)
        {
            await command.FollowupAsync("ℹ️ You don't have an expedition in progress. Use `/expedition start` to send cards off.", ephemeral: true);
            return;
        }

        var now = DateTime.UtcNow;

        if (now < expedition.EndTimeUtc)
        {
            var endUnix = new DateTimeOffset(expedition.EndTimeUtc).ToUnixTimeSeconds();
            await command.FollowupAsync($"⏳ Your expedition isn't back yet. Returns <t:{endUnix}:R>.", ephemeral: true);
            return;
        }

        var location = ExpeditionSettingsProvider.GetLocationById(expedition.LocationId);

        // Roll coin reward
        double coinReward = location == null
            ? 0.0
            : location.MinReward + _random.NextDouble() * (location.MaxReward - location.MinReward);

        collection.Balance += coinReward;

        var rewardText = $"💰 **{coinReward:F2}** coins";

        // Roll card reward
        List<Card> rewardCards = [];
        if (location != null && _random.NextDouble() < location.CardRewardChance)
        {
            var setId = string.IsNullOrWhiteSpace(location.CardRewardSetId)
                ? await GetRandomAvailableSetId()
                : location.CardRewardSetId;

            if (!string.IsNullOrWhiteSpace(setId))
            {
                rewardCards = await CommandHandler.GetRandomCards(location.CardRewardCount, setId, "en");
                collection.Cards.AddRange(rewardCards);
            }
        }

        if (rewardCards.Count > 0)
        {
            rewardText += "\n🃏 " + string.Join(", ", rewardCards.Select(c => $"`{c.Name}` ({c.Rarity})"));
        }

        // Unlock the cards that were sent on the expedition (matched by Name+Rarity against the
        // still-locked entries, by index so duplicate copies each get unlocked exactly once).
        var unlockedIndices = new HashSet<int>();
        foreach (var sentCard in expedition.SentCards)
        {
            var matchIndex = collection.Cards.FindIndex(c =>
                c.IsLocked && c.Name == sentCard.Name && c.Rarity == sentCard.Rarity);

            while (matchIndex != -1 && unlockedIndices.Contains(matchIndex))
            {
                matchIndex = collection.Cards.FindIndex(matchIndex + 1, c =>
                    c.IsLocked && c.Name == sentCard.Name && c.Rarity == sentCard.Rarity);
            }

            if (matchIndex == -1) continue;

            collection.Cards[matchIndex].IsLocked = false;
            unlockedIndices.Add(matchIndex);
        }

        // Reset the expedition so a new one can be started
        collection.ActiveExpedition = null;

        await CardStorage.SaveUserCardsAsync(collection);

        await command.FollowupAsync($"✅ Expedition rewards claimed!\n{rewardText}", ephemeral: true);
    }

    /// <summary>
    ///     Picks a random, non-locked set id to draw a card reward from when a location doesn't
    ///     configure a fixed <see cref="ExpeditionLocation.CardRewardSetId"/>.
    /// </summary>
    private static async Task<string?> GetRandomAvailableSetId()
    {
        try
        {
            var response = await CommandHandler._httpClient.GetStringAsync(CommandHandler.SetsApiUrl);
            var sets = JsonConvert.DeserializeObject<List<Set>>(response);

            var availableSetIds = sets?
                .Where(s => !CommandHandler.LockedSets.Contains(s.Id))
                .Select(s => s.Id)
                .ToList();

            if (availableSetIds == null || availableSetIds.Count == 0) return null;

            return availableSetIds[_random.Next(availableSetIds.Count)];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to pick random expedition reward set: {ex.Message}");
            return null;
        }
    }
}
