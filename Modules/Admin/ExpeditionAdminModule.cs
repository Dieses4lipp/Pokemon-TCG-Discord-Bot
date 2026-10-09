using System.Globalization;
using Discord;
using Discord.Interactions;
using DiscordBot.Core;
using DiscordBot.Models;
using DiscordBot.Modules.Expedition;
using DiscordBot.Preconditions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Modules.Admin;

/// <summary>
///     Owner-only /expedition-admin commands: change the expedition locations at runtime and look
///     into a user's running expedition.
/// </summary>
[Group("expedition-admin", "Manage expedition locations and running expeditions. (Admin only)")]
[DefaultMemberPermissions(GuildPermission.Administrator)]
[RequireBotOwner]
public sealed class ExpeditionAdminModule(
    ExpeditionLocationStore locations,
    UserRepository users,
    ILogger<ExpeditionAdminModule> logger) : InteractionModuleBase<SocketInteractionContext>
{
    /// <summary>
    ///     Value of <c>card-reward-set-id</c> that switches a location back to a random set.
    /// </summary>
    private const string RandomSetId = "random";

    [SlashCommand("list", "Lists all expedition locations and their rewards.")]
    public async Task ListAsync()
    {
        await DeferAsync(ephemeral: true);

        if (locations.Locations.Count == 0)
        {
            await FollowupAsync("ℹ️ There are no expedition locations.");
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🧭 Expedition locations")
            .WithColor(Color.Blue);

        // An embed takes at most 25 fields
        foreach (var location in locations.Locations.OrderBy(l => l.DurationMinutes).Take(25))
            embed.AddField($"{location.Name} (`{location.Id}`)", Describe(location));

        await FollowupAsync(embed: embed.Build());
    }

    [SlashCommand("edit", "Changes a location; options left out keep their value.")]
    public async Task EditAsync(
        [Summary("location", "The location to change."), Autocomplete(typeof(ExpeditionLocationAutocompleteHandler))] string locationId,
        [Summary("name", "Display name.")] string? name = null,
        [Summary("duration-minutes", "How long the expedition takes.")] int? durationMinutes = null,
        [Summary("min-reward", "Smallest coin reward.")] double? minReward = null,
        [Summary("max-reward", "Largest coin reward.")] double? maxReward = null,
        [Summary("card-reward-chance", "Chance of a card reward, 0 to 1.")] double? cardRewardChance = null,
        [Summary("card-reward-count", "Cards awarded when the card reward triggers.")] int? cardRewardCount = null,
        [Summary("card-reward-set-id", "Set to draw reward cards from, or 'random'.")] string? cardRewardSetId = null)
    {
        await DeferAsync(ephemeral: true);

        var current = locations.GetLocationById(locationId);
        if (current == null)
        {
            await FollowupAsync($"❌ Unknown expedition location `{locationId}`.");
            return;
        }

        // A new object: the store's copy must not change unless the save succeeds
        var edited = new ExpeditionLocation
        {
            Id = current.Id,
            Name = name?.Trim() ?? current.Name,
            DurationMinutes = durationMinutes ?? current.DurationMinutes,
            MinReward = minReward ?? current.MinReward,
            MaxReward = maxReward ?? current.MaxReward,
            CardRewardChance = cardRewardChance ?? current.CardRewardChance,
            CardRewardCount = cardRewardCount ?? current.CardRewardCount,
            CardRewardSetId = cardRewardSetId == null
                ? current.CardRewardSetId
                : cardRewardSetId.Trim().Equals(RandomSetId, StringComparison.OrdinalIgnoreCase) ? null : cardRewardSetId.Trim(),
        };

        if (ExpeditionLocationRules.Validate(edited) is { } error)
        {
            await FollowupAsync($"❌ Not saved: {error}.");
            return;
        }

        await locations.SaveAsync(edited);

        logger.LogInformation("Admin {AdminId} changed expedition location {LocationId} from [{Before}] to [{After}]",
            Context.User.Id, edited.Id, Describe(current), Describe(edited));
        await FollowupAsync($"✅ **{edited.Name}** (`{edited.Id}`) saved.\n{Describe(edited)}\n" +
                            "Running expeditions keep their end time; rewards use the new values on claim.");
    }

    [SlashCommand("reload", "Resets all locations to Data/expeditionSettings.json, undoing every edit.")]
    public async Task ReloadAsync()
    {
        await DeferAsync(ephemeral: true);

        int count;
        try
        {
            count = await locations.ReloadFromFileAsync();
        }
        catch (InvalidDataException ex)
        {
            logger.LogError(ex, "Admin {AdminId} failed to reload the expedition locations", Context.User.Id);
            await FollowupAsync($"❌ Nothing changed, the settings file is invalid: {ex.Message}");
            return;
        }

        logger.LogInformation("Admin {AdminId} reset the expedition locations to the settings file ({Count} locations)",
            Context.User.Id, count);
        await FollowupAsync($"🔄 Reset to the settings file: {count} location(s).");
    }

    [SlashCommand("inspect", "Shows a user's running expedition.")]
    public async Task InspectAsync(
        [Summary("user", "The user to look up.")] IUser user)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);
        if (collection.ActiveExpedition is not { } expedition)
        {
            await FollowupAsync($"ℹ️ {user.Username} has no expedition running.");
            return;
        }

        var location = locations.GetLocationById(expedition.LocationId);
        var cards = string.Join("\n", collection.CardsOnExpedition.Select(c => $"`{c.Name}` ({c.Rarity})"));

        var embed = new EmbedBuilder()
            .WithTitle($"🔎 {user.Username}'s expedition")
            .AddField("Location", location == null ? $"`{expedition.LocationId}` (no longer exists, claim pays nothing)" : $"{location.Name} (`{location.Id}`)")
            .AddField("Started", $"<t:{ToUnix(expedition.StartTimeUtc)}:f>", true)
            .AddField("Returns", $"<t:{ToUnix(expedition.EndTimeUtc)}:R>", true)
            .AddField("Cards sent", string.IsNullOrEmpty(cards) ? "—" : cards)
            .WithColor(Color.Orange)
            .Build();

        await FollowupAsync(embed: embed);
    }

    [SlashCommand("finish", "Ends a user's running expedition now, so it can be claimed right away.")]
    public async Task FinishAsync(
        [Summary("user", "The user whose expedition to finish.")] IUser user)
    {
        await DeferAsync(ephemeral: true);

        var collection = await users.LoadUserCardsAsync(user.Id);
        if (collection.ActiveExpedition is not { } expedition)
        {
            await FollowupAsync($"ℹ️ {user.Username} has no expedition running.");
            return;
        }

        var now = DateTime.UtcNow;
        if (expedition.EndTimeUtc <= now)
        {
            await FollowupAsync($"ℹ️ {user.Username}'s expedition is already back and waiting for `/expedition claim`.");
            return;
        }

        var originalEnd = expedition.EndTimeUtc;
        expedition.EndTimeUtc = now;
        await users.SaveUserCardsAsync(collection);

        logger.LogInformation("Admin {AdminId} finished the expedition of user {UserId} to {LocationId} early (was due {OriginalEndUtc:O})",
            Context.User.Id, user.Id, expedition.LocationId, originalEnd);
        await FollowupAsync($"⏩ {user.Mention}'s expedition is back, it can be claimed with `/expedition claim`.");
    }

    private static string Describe(ExpeditionLocation location) => string.Create(CultureInfo.InvariantCulture,
        $"{location.DurationMinutes} min, 💰 {location.MinReward:F2}–{location.MaxReward:F2}, " +
        $"🃏 {location.CardRewardChance:P0} for {location.CardRewardCount} card(s) from " +
        $"{(string.IsNullOrWhiteSpace(location.CardRewardSetId) ? "a random set" : $"`{location.CardRewardSetId}`")}");

    private static long ToUnix(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
}
