using System.Diagnostics;
using DiscordBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Core;

/// <summary>
///     Talks to the TCGdex card API.
/// </summary>
public sealed class CardApiClient
{
    private const string ApiLangUrl = "https://api.tcgdex.net/v2/";
    private const string SetsApiUrl = "https://api.tcgdex.net/v2/en/sets";

    /// <summary>
    ///     How many card detail requests may run against the API at the same time.
    /// </summary>
    private const int MaxParallelCardRequests = 8;

    private readonly HttpClient _httpClient = new();

    /// <summary>
    ///     The latency of the last set request.
    /// </summary>
    public long LastApiLatency { get; private set; }

    /// <summary>
    ///     Fetches all English sets.
    /// </summary>
    /// <returns>
    ///     The sets, or an empty list if the API returned none.
    /// </returns>
    public Task<List<Set>> GetSetsAsync() => GetSetsFromAsync(SetsApiUrl);

    /// <summary>
    ///     Fetches the first page of English sets.
    /// </summary>
    /// <param name="itemsPerPage">
    ///     The page size.
    /// </param>
    /// <returns>
    ///     The sets, or an empty list if the API returned none.
    /// </returns>
    public Task<List<Set>> GetFirstSetsAsync(int itemsPerPage) =>
        GetSetsFromAsync($"{SetsApiUrl}?pagination:page=1&pagination:itemsPerPage={itemsPerPage}");

    private async Task<List<Set>> GetSetsFromAsync(string url)
    {
        string response = await _httpClient.GetStringAsync(url);
        return JsonConvert.DeserializeObject<List<Set>>(response) ?? [];
    }

    /// <summary>
    ///     Retrieves a list of random Pokémon cards of a set from the API.
    /// </summary>
    /// <param name="count">
    ///     The number of cards to retrieve.
    /// </param>
    /// <param name="setId">
    ///     The set ID to draw cards from.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     A task that returns a list of <see cref="Card"/> objects.
    /// </returns>
    public async Task<List<Card>> GetRandomCardsAsync(int count, string setId, string language)
    {
        var cardIds = await FetchSetCardIdsAsync(setId, language);
        var selectedIds = cardIds.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        return await FetchCardDetailsAsync(selectedIds, language);
    }

    /// <summary>
    ///     Fetches the IDs of all cards in a set.
    /// </summary>
    /// <param name="setId">
    ///     The set ID.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     The card IDs of the set, or an empty list if the request failed.
    /// </returns>
    public async Task<List<string>> FetchSetCardIdsAsync(string setId, string language)
    {
        string requestUrl = $"{ApiLangUrl}{language}/sets/{setId}";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            string response = await _httpClient.GetStringAsync(requestUrl);
            stopwatch.Stop();
            LastApiLatency = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"API Latency: {LastApiLatency}ms");

            // Parse into JToken and try to locate an array of card briefs/ids
            var token = JToken.Parse(response);

            // Helper: find candidate array containing card objects or ids
            JArray? cardArray = token.Type == JTokenType.Array ? (JArray)token : null;
            if (cardArray == null)
            {
                // common wrappers: data, cards, sets
                var candidates = new[] { "data", "cards", "results", "items", "sets" };
                foreach (var name in candidates)
                {
                    var candidate = token[name];
                    if (candidate != null && candidate.Type == JTokenType.Array)
                    {
                        cardArray = (JArray)candidate;
                        break;
                    }
                }
            }

            // If we still don't have an array and the response is an object with many properties,
            // try to find the first array property
            if (cardArray == null && token.Type == JTokenType.Object)
            {
                var firstArray = ((JObject)token).Properties().FirstOrDefault(p => p.Value.Type == JTokenType.Array);
                if (firstArray != null) cardArray = (JArray)firstArray.Value;
            }

            if (cardArray == null)
            {
                Console.WriteLine("No card array found in API response.");
                return [];
            }

            // Map array items to card briefs (extract id field)
            var cardIds = cardArray
                .Select(item =>
                {
                    // item might be string id, or object with id property
                    if (item.Type == JTokenType.String) return (string?)item.Value<string>();
                    var idToken = item["id"] ?? item["cardId"] ?? item["uuid"];
                    return idToken?.Value<string>();
                })
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => id!)
                .ToList();

            if (cardIds.Count == 0)
            {
                Console.WriteLine("No card ids found in card array.");
            }

            return cardIds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to get cards of set {setId}: {ex}");
            return [];
        }
    }

    /// <summary>
    ///     Fetches the full details of several cards, a limited number of requests at a time.
    ///     Cards that fail to load are skipped.
    /// </summary>
    /// <param name="cardIds">
    ///     The IDs of the cards to fetch.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     The successfully fetched cards, in the order of <paramref name="cardIds"/>.
    /// </returns>
    public async Task<List<Card>> FetchCardDetailsAsync(IReadOnlyList<string> cardIds, string language)
    {
        using var throttle = new SemaphoreSlim(MaxParallelCardRequests);

        var cards = await Task.WhenAll(cardIds.Select(async id =>
        {
            await throttle.WaitAsync();
            try
            {
                return await FetchCardDetailAsync(id, language);
            }
            finally
            {
                throttle.Release();
            }
        }));

        return cards.OfType<Card>().ToList();
    }

    /// <summary>
    ///     Fetches the full details of a single card.
    /// </summary>
    /// <param name="id">
    ///     The card ID.
    /// </param>
    /// <param name="language">
    ///     The API language code, e.g. "en".
    /// </param>
    /// <returns>
    ///     The card, or <see langword="null"/> if it could not be fetched.
    /// </returns>
    private async Task<Card?> FetchCardDetailAsync(string id, string language)
    {
        try
        {
            var url = $"{ApiLangUrl}{language}/cards/{id}";
            // Fetch detailed card. Some APIs return an object wrapper; handle both.
            var detailedResponse = await _httpClient.GetStringAsync(url);
            var detailToken = JToken.Parse(detailedResponse);

            // find inner object with id/name/images or fall back to top-level
            JToken? cardToken = null;
            if (detailToken.Type == JTokenType.Object)
            {
                cardToken = detailToken["data"] ?? detailToken["card"] ?? detailToken;
            }
            else if (detailToken.Type == JTokenType.Array)
            {
                cardToken = detailToken.First;
            }

            return cardToken?.ToObject<Card>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch detailed card {id}: {ex.Message}");
            return null;
        }
    }
}
