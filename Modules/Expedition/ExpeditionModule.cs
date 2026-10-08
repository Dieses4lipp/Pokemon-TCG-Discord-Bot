using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Events.Expedition;
using DiscordBot.Models;
using DiscordBot.Preconditions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Modules.Expedition;

/// <summary>
///     The /expedition start, status and claim subcommands.
/// </summary>
[Group("expedition", "Send cards on an expedition.")]
[RequireBotActive]
public sealed class ExpeditionModule(
    BotState botState,
    UserRepository users,
    CardApiClient api,
    ILogger<ExpeditionModule> logger)
    : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly Random _random = new();

    /// <summary>
    ///     Sends cards off, after validating card ownership and that no expedition is already running.
    /// </summary>
    [SlashCommand("start", "Starts a new expedition.")]
    public async Task StartAsync(
        [Summary("location", "The location to send your cards to."), Autocomplete(typeof(ExpeditionLocationAutocompleteHandler))]
        string locationId,
        [Summary("cards", "Comma-separated names of the cards to send."), Autocomplete(typeof(ExpeditionCardsAutocompleteHandler))]
        string cardsInput)
    {
        await DeferAsync(ephemeral: true);

        var location = string.IsNullOrWhiteSpace(locationId)
            ? null
            : ExpeditionSettingsProvider.GetLocationById(locationId);

        if (location == null)
        {
            await FollowupAsync($"❌ Unknown expedition location `{locationId}`.", ephemeral: true);
            return;
        }

        var collection = await users.LoadUserCardsAsync(Context.User.Id);

        // No expedition already running
        if (collection.ActiveExpedition != null)
        {
            await FollowupAsync("⚠️ You already have an expedition in progress. Use `/expedition status` to check on it.", ephemeral: true);
            return;
        }

        var requestedNames = (cardsInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (requestedNames.Count == 0)
        {
            await FollowupAsync("❌ You must specify at least one card to send.", ephemeral: true);
            return;
        }

        if (!ExpeditionCards.TrySelect(collection.Cards, requestedNames, out var selectedCards, out var missingName))
        {
            await FollowupAsync($"❌ You don't own an available (unlocked) card named `{missingName}`.", ephemeral: true);
            return;
        }

        // Lock the selected cards so they can't be sold/traded while away
        foreach (var card in selectedCards)
        {
            card.IsLocked = true;
        }

        var startTime = DateTime.UtcNow;
        var endTime = startTime.AddMinutes(location.DurationMinutes);

        collection.ActiveExpedition = new ActiveExpedition
        {
            LocationId = location.Id,
            StartTimeUtc = startTime,
            EndTimeUtc = endTime,
        };

        await users.SaveUserCardsAsync(collection);

        await FollowupAsync(
            $"✅ Sent {selectedCards.Count} card(s) on an expedition to **{location.Name}**!\n" +
            $"Returns <t:{new DateTimeOffset(endTime).ToUnixTimeSeconds()}:R>.",
            ephemeral: true);
    }

    /// <summary>
    ///     Shows the remaining time (or claim readiness) of the user's active expedition.
    /// </summary>
    [SlashCommand("status", "Shows the status of your current expedition.")]
    public async Task StatusAsync()
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(Context.User.Id);
        var expedition = collection.ActiveExpedition;

        if (expedition == null)
        {
            await FollowupAsync("ℹ️ You don't have an expedition in progress. Use `/expedition start` to send cards off.", ephemeral: true);
            return;
        }

        var location = ExpeditionSettingsProvider.GetLocationById(expedition.LocationId);
        var locationName = location?.Name ?? expedition.LocationId;
        var endUnix = new DateTimeOffset(expedition.EndTimeUtc).ToUnixTimeSeconds();

        var cardList = string.Join("\n", collection.CardsOnExpedition.Select(c => $"`{c.Name}` ({c.Rarity})"));

        var remaining = expedition.EndTimeUtc - DateTime.UtcNow;

        var embed = new EmbedBuilder()
            .WithTitle($"🧭 Expedition to {locationName}")
            .AddField("Cards sent", string.IsNullOrEmpty(cardList) ? "—" : cardList)
            .AddField(
                remaining <= TimeSpan.Zero ? "Status" : "Time remaining",
                remaining <= TimeSpan.Zero
                    ? "✅ Ready to claim! Use `/expedition claim`."
                    : $"<t:{endUnix}:R> (<t:{endUnix}:f>)")
            .WithColor(remaining <= TimeSpan.Zero ? Color.Green : Color.Orange)
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    /// <summary>
    ///     Grants the rewards of a finished expedition and unlocks the cards that were sent.
    /// </summary>
    [SlashCommand("claim", "Claims the rewards of a finished expedition.")]
    public async Task ClaimAsync()
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(Context.User.Id);
        var expedition = collection.ActiveExpedition;

        if (expedition == null)
        {
            await FollowupAsync("ℹ️ You don't have an expedition in progress. Use `/expedition start` to send cards off.", ephemeral: true);
            return;
        }

        var now = DateTime.UtcNow;

        if (now < expedition.EndTimeUtc)
        {
            var endUnix = new DateTimeOffset(expedition.EndTimeUtc).ToUnixTimeSeconds();
            await FollowupAsync($"⏳ Your expedition isn't back yet. Returns <t:{endUnix}:R>.", ephemeral: true);
            return;
        }

        var location = ExpeditionSettingsProvider.GetLocationById(expedition.LocationId);

        // Roll coin reward
        decimal coinReward = location == null
            ? 0m
            : Money.FromDouble(location.MinReward + _random.NextDouble() * (location.MaxReward - location.MinReward));


        collection.Balance += coinReward;

        var rewardText = $"💰 **{coinReward:F2}** coins";

        // Roll card reward
        List<Card> rewardCards = [];
        if (location != null && _random.NextDouble() < location.CardRewardChance)
        {
            var setId = string.IsNullOrWhiteSpace(location.CardRewardSetId)
                ? await GetRandomAvailableSetIdAsync()
                : location.CardRewardSetId;

            if (!string.IsNullOrWhiteSpace(setId))
            {
                rewardCards = await api.GetRandomCardsAsync(location.CardRewardCount, setId, "en");
                collection.Cards.AddRange(rewardCards);
            }
        }

        if (rewardCards.Count > 0)
        {
            rewardText += "\n🃏 " + string.Join(", ", rewardCards.Select(c => $"`{c.Name}` ({c.Rarity})"));
        }

        foreach (var card in collection.CardsOnExpedition.ToList())
            card.IsLocked = false;

        // Reset the expedition so a new one can be started
        collection.ActiveExpedition = null;

        await users.SaveUserCardsAsync(collection);

        await FollowupAsync($"✅ Expedition rewards claimed!\n{rewardText}", ephemeral: true);
    }

    /// <summary>
    ///     Picks a random, non-locked set id to draw a card reward from when a location doesn't
    ///     configure a fixed <see cref="ExpeditionLocation.CardRewardSetId"/>.
    /// </summary>
    private async Task<string?> GetRandomAvailableSetIdAsync()
    {
        try
        {
            var availableSetIds = (await api.GetSetsAsync())
                .Where(s => !botState.IsSetLocked(s.Id))
                .Select(s => s.Id)
                .ToList();

            if (availableSetIds.Count == 0) return null;

            return availableSetIds[_random.Next(availableSetIds.Count)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pick random expedition reward set");
            return null;
        }
    }
}
