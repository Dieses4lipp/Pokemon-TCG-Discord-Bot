using System.Diagnostics;
using Discord;
using Discord.WebSocket;
using DiscordBot.Models;
using Newtonsoft.Json.Linq;

namespace DiscordBot.Core;

/// <summary>
///     Handles the registration of commands for the bot.
/// </summary>
public static class CommandHandler
{
    public static readonly HttpClient _httpClient = new();
    public static readonly string CardsApiUrl = "https://api.tcgdex.net/v2/en/cards";
    public static readonly string ApiLangUrl = "https://api.tcgdex.net/v2/";
    public static readonly string SetsApiUrl = "https://api.tcgdex.net/v2/en/sets";

    /// <summary>
    ///     Stores active card navigation sessions mapped by message ID.
    /// </summary>
    public static readonly Dictionary<ulong, PackSession> ActiveSessions = [];

    /// <summary>
    ///     Gets a dictionary that contains the currently active trade sessions, indexed by their
    ///     unique identifiers.
    /// </summary>
    public static readonly Dictionary<ulong, TradeSession> ActiveTrades = [];

    /// <summary>
    ///     Stores active set navigation sessions mapped by message ID.
    /// </summary>
    public static readonly Dictionary<ulong, SetSession> ActiveSetSessions = [];

    /// <summary>
    ///     Stores the locked sets to prevent them from being pulled.
    /// </summary>
    public static readonly HashSet<string> LockedSets = [];

    /// <summary>
    ///     Indicates whether the bot is active and responding to commands.
    /// </summary>
    public static bool BotActive = true;

    public static int SetsSessionIndex = 0;

    /// <summary>
    ///     The number of cards that have been pulled by all users.
    /// </summary>
    public static int PullCount { get; set; } = 0;

    /// <summary>
    ///     The Last took Api Latency.
    /// </summary>
    public static long LastApiLatency { get; private set; } = 0;

    /// <summary>
    ///     How many card detail requests may run against the API at the same time.
    /// </summary>
    private const int MaxParallelCardRequests = 8;

    /// <summary>
    ///     Image shown for cards the API delivers without an image.
    /// </summary>
    private static readonly string DefaultCardImagePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "sets_covers", "default.jpg");

    /// <summary>
    ///     Builds the attachments a card embed from <see cref="BuildCardEmbed"/> needs.
    ///     Must be passed on every send/modify, so a previous card's fallback image gets removed.
    /// </summary>
    /// <param name="card">
    ///     The card to display.
    /// </param>
    /// <returns>
    ///     The fallback image if the card has no image URL, otherwise no attachments.
    ///     The caller disposes them after sending.
    /// </returns>
    public static FileAttachment[] BuildCardAttachments(Card card)
    {
        return string.IsNullOrWhiteSpace(card.Image)
            ? [new FileAttachment(DefaultCardImagePath)]
            : [];
    }

    /// <summary>
    ///     Builds an embed to display a Pokémon card.
    /// </summary>
    /// <param name="card">
    ///     The card to display.
    /// </param>
    /// <param name="current">
    ///     The current index of the card within a session.
    /// </param>
    /// <param name="total">
    ///     The total number of cards in the session.
    /// </param>
    /// <returns>
    ///     An <see cref="Embed"/> representing the card's details.
    /// </returns>
    public static Embed BuildCardEmbed(Card card, int current, int total)
    {
        double marketPrice = 
            card.Pricing?.TcgPlayer?.Market ?? 
            card.Pricing?.TcgPlayer?.Low ?? 
            card.Pricing?.Cardmarket?.Avg ?? 
            0.50;
        // Discord only accepts URLs here; the local fallback is sent as an attachment (see BuildCardAttachments)
        string cardImageUrl = string.IsNullOrWhiteSpace(card.Image)
            ? $"attachment://{Path.GetFileName(DefaultCardImagePath)}"
            : $"{card.Image}/low.png";

        return new EmbedBuilder()
            .WithTitle($"{card.Name} ({current}/{total})")
            .WithDescription($"Rarity: {card.Rarity ?? "unknown"}\nEstimated Value: **${marketPrice:F2}**")
            .WithImageUrl(cardImageUrl)
            .WithColor(Color.Blue)
            .Build();
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
    public static async Task<List<Card>> GetRandomCards(int count, string setId, string language)
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
    public static async Task<List<string>> FetchSetCardIdsAsync(string setId, string language)
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
    public static async Task<List<Card>> FetchCardDetailsAsync(IReadOnlyList<string> cardIds, string language)
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
    private static async Task<Card?> FetchCardDetailAsync(string id, string language)
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

    /// <summary>
    ///     Handles cleanup when a user leaves a guild by archiving their saved card collection.
    ///     The collection is restored on the user's next command, so leaving one guild while still
    ///     playing in another (or rejoining) does not lose any data.
    /// </summary>
    /// <param name="user">
    ///     The user who left.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static Task HandleUserLeft(SocketGuild _, SocketUser user)
    {
        if (CardStorage.ArchiveUserCards(user.Id))
        {
            Console.WriteLine($"Archived JSON file for user {user.Username} ({user.Id}).");
        }
        else
        {
            Console.WriteLine($"No JSON file found for user {user.Username} ({user.Id}).");
        }

        return Task.CompletedTask;
    }
}