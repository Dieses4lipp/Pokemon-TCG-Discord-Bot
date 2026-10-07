using System.Collections.Concurrent;
using DiscordBot.Models;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     Caches the full card list of a set per language in memory, so a pack pull only hits the
///     card API once per set and cache period instead of once per card on every pull.
/// </summary>
public sealed class SetCardCache(CardApiClient api, ILogger<SetCardCache> logger)
{
    /// <summary>
    ///     How long a loaded set stays cached; also bounds how stale card prices can get.
    /// </summary>
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    // Stores the running or finished load per "language/setId", so concurrent pulls of the same
    // set share one load instead of each fetching every card
    private readonly ConcurrentDictionary<string, Task<(DateTime LoadedAtUtc, List<Card> Cards)>> Cache = new();

    /// <summary>
    ///     Gets all cards of a set, loading them from the API on first use or after the cache
    ///     period expired. Failed or empty loads are not cached.
    /// </summary>
    /// <param name="setId">
    ///     The set ID.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     The cards of the set, or an empty list if the set could not be loaded.
    /// </returns>
    public async Task<List<Card>> GetSetCardsAsync(string setId, string language)
    {
        string key = $"{language}/{setId}";

        var load = Cache.GetOrAdd(key, _ => LoadAsync(setId, language));
        var (loadedAtUtc, cards) = await load;

        if (cards.Count > 0 && DateTime.UtcNow - loadedAtUtc <= CacheDuration)
            return cards;

        // Drop the failed or expired entry (only if no one replaced it meanwhile)
        Cache.TryRemove(new KeyValuePair<string, Task<(DateTime, List<Card>)>>(key, load));

        if (cards.Count == 0)
            return cards;

        return (await Cache.GetOrAdd(key, _ => LoadAsync(setId, language))).Cards;
    }

    /// <summary>
    ///     Loads every card of a set from the API.
    /// </summary>
    /// <param name="setId">
    ///     The set ID.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     The load time and the cards of the set.
    /// </returns>
    private async Task<(DateTime LoadedAtUtc, List<Card> Cards)> LoadAsync(string setId, string language)
    {
        var cardIds = await api.FetchSetCardIdsAsync(setId, language);
        var cards = await api.FetchCardDetailsAsync(cardIds, language);

        logger.LogInformation("Loaded {LoadedCount}/{CardCount} cards of set '{SetId}' ({Language}) into the cache",
            cards.Count, cardIds.Count, setId, language);
        return (DateTime.UtcNow, cards);
    }
}
