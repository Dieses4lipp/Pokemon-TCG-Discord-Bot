using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Models;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Handles the registration of commands for the bot.
/// </summary>
public static class CommandHandler
{
    public static readonly HttpClient _httpClient = new();
    public static readonly string ApiUrl = "https://api.pokemontcg.io/v2/cards";
    public static readonly string SetsApiUrl = "https://api.pokemontcg.io/v2/sets";

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
    ///     Maps card rarities to their corresponding probabilities.
    /// </summary>
    public static readonly Dictionary<string, double> RarityChances = new()
    {
        {"Common", 0.50 },
        {"Uncommon", 0.20 },
        {"Rare", 0.15 },
        {"Rare Holo", 0.05 },
        {"Ultra Rare", 0.07 },
        {"Secret Rare", 0.03 }
    };

    /// <summary>
    ///     Stores the locked sets to prevent them from being pulled.
    /// </summary>
    public static readonly HashSet<string> LockedSets = [];

    /// <summary>
    ///     Indicates whether the bot is active and responding to commands.
    /// </summary>
    public static bool BotActive = true;

    /// <summary>
    ///     The number of cards that have been pulled by all users.
    /// </summary>
    public static int PullCount { get; set; } = 0;

    /// <summary>
    ///     The Last took Api Latency.
    /// </summary>
    public static long LastApiLatency { get; private set; } = 0;

    /// <summary>
    ///     Registers all commands within the assembly.
    /// </summary>
    /// <param name="services">
    ///     The service provider used to resolve services.
    /// </param>
    /// <returns>
    ///     A task representing the asynchronous operation.
    /// </returns>
    public static async Task RegisterCommandsAsync(IServiceProvider services)
    {
        var commandService = services.GetRequiredService<CommandService>();
        await commandService.AddModulesAsync(Assembly.GetEntryAssembly(), services);
    }

    public static void LogActiveTrades()
    {
        Console.WriteLine("Active Trades:");
        foreach (var trade in ActiveTrades)
        {
            Console.WriteLine($"Sender: {trade.Value.SenderId}, Receiver: {trade.Value.ReceiverId}, Card: {trade.Value.CardToTrade.Name}");
        }
    }

    /// <summary>
    ///     Determines the rarity of a card based on predefined probabilities.
    /// </summary>
    /// <param name="random">
    ///     An instance of the random number generator.
    /// </param>
    /// <returns>
    ///     A string representing the selected rarity.
    /// </returns>
    public static string RollRarity(Random random)
    {
        double roll = random.NextDouble();
        double cumulative = 0.0;

        foreach (var rarity in RarityChances)
        {
            cumulative += rarity.Value;
            if (roll <= cumulative)
            {
                return rarity.Key;
            }
        }

        return "Common";
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
        return new EmbedBuilder()
            .WithTitle($"{card.Name} ({current}/{total})")
            .WithDescription($"Rarity: {card.Rarity}")
            .WithImageUrl(card.Images.Small)
            .WithColor(Color.Blue)
            .Build();
    }

    /// <summary>
    ///     Handles reactions added to a card embed message to navigate or modify the user's card collection.
    /// </summary>
    /// <param name="cache">
    ///     The cached user message.
    /// </param>
    /// <param name="channel">
    ///     The channel where the message was sent.
    /// </param>
    /// <param name="reaction">
    ///     The reaction that was added.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        if (reaction.User.Value.IsBot)
        {
            return;
        }

        if (!ActiveSessions.TryGetValue(reaction.MessageId, out PackSession? session))
        {
            return;
        }

        if (reaction.UserId != session.UserId) return;

        IUserMessage? message = await cache.GetOrDownloadAsync();

        if (reaction.Emote.Name == "▶️" || reaction.Emote.Name == "◀️")
        {
            // Navigate through cards.
            session.CurrentIndex = reaction.Emote.Name == "▶️"
                ? (session.CurrentIndex + 1) % session.Cards.Count
                : (session.CurrentIndex - 1 + session.Cards.Count) % session.Cards.Count;

            await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));
        }
        else if (reaction.Emote.Name == "💾")
        {
            // Save the current card.
            Card cardToSave = session.Cards[session.CurrentIndex];
            UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

            var cardIdentifier = $"{cardToSave.Name}_{cardToSave.Rarity}";
            if (session.SavedCardIdentifiers.Contains(cardIdentifier))
            {
                IMessageChannel? msgCHannel = await channel.GetOrDownloadAsync();
                await msgCHannel.SendMessageAsync("You already have this card in your collection");
                return;
            }

            session.SavedCardIdentifiers.Add(cardIdentifier);

            if (collection.Cards.Count >= 10)
            {
                IMessageChannel? msgChannel = await channel.GetOrDownloadAsync();
                await msgChannel.SendMessageAsync("You can only store up to 10 cards! Try !mycards and delete a card to make room!");
                return;
            }

            collection.Cards.Add(cardToSave);
            await CardStorage.SaveUserCardsAsync(collection);

            IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
            await channelForMsg.SendMessageAsync($"Card '{cardToSave.Name}' has been saved to your collection!");
        }
        else if (reaction.Emote.Name == "🗑️")
        {
            // Delete the current card from the collection.
            Card cardToDelete = session.Cards[session.CurrentIndex];
            UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

            var cardIdentifier = $"{cardToDelete.Name}_{cardToDelete.Rarity}";
            session.SavedCardIdentifiers.Remove(cardIdentifier);
            int removedCount = collection.Cards.RemoveAll(c => c.Name == cardToDelete.Name && c.Rarity == cardToDelete.Rarity);
            if (removedCount > 0)
            {
                await CardStorage.SaveUserCardsAsync(collection);
                session.Cards.RemoveAt(session.CurrentIndex);

                if (session.Cards.Count == 0)
                {
                    await message.DeleteAsync();
                    ActiveSessions.Remove(message.Id);
                    return;
                }

                if (session.CurrentIndex >= session.Cards.Count)
                {
                    session.CurrentIndex = session.Cards.Count - 1;
                }

                await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));

                IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
                await channelForMsg.SendMessageAsync($"Card '{cardToDelete.Name}' has been removed from your collection!");
            }
            else
            {
                IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
                await channelForMsg.SendMessageAsync("Card not found in your collection. Did you save a card with the save symbol next to the Card?");
            }
        }
        else if (reaction.Emote.Name == "⭐")
        {
            Card favoriteCard = session.Cards[session.CurrentIndex];
            UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);
            collection.FavoriteCard = favoriteCard;
            await CardStorage.SaveUserCardsAsync(collection);
            IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
            await channelForMsg.SendMessageAsync($"Your favorite card has been set to '{favoriteCard.Name}'!");
        }

        // Remove the reaction after processing.
        await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
    }

    /// <summary>
    ///     Saves the specified user's card collection to a JSON file.
    /// </summary>
    /// <param name="collection">
    ///     The user card collection to save.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static async Task SaveUserCardsAsync(UserCardCollection collection)
    {
        string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{collection.UserId}.json");
        string json = JsonConvert.SerializeObject(collection, Formatting.Indented);
        await File.WriteAllTextAsync(userFilePath, json);
    }

    /// <summary>
    ///     Loads the card collection for the specified user from a JSON file. If the file does not
    ///     exist, returns an empty collection.
    /// </summary>
    /// <param name="userId">
    ///     The user's ID.
    /// </param>
    /// <returns>
    ///     A task that returns a <see cref="UserCardCollection"/> representing the user's saved cards.
    /// </returns>
    public static async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
    {
        string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{userId}.json");
        if (File.Exists(userFilePath))
        {
            string json = await File.ReadAllTextAsync(userFilePath);
            return JsonConvert.DeserializeObject<UserCardCollection>(json) ?? new UserCardCollection();
        }

        return new UserCardCollection { UserId = userId, Cards = [] };
    }

    /// <summary>
    ///     Retrieves a list of random Pokémon cards from the API. Optionally filters by set ID.
    /// </summary>
    /// <param name="count">
    ///     The number of cards to retrieve.
    /// </param>
    /// <param name="setId">
    ///     The set ID to filter cards by (optional).
    /// </param>
    /// <returns>
    ///     A task that returns a list of <see cref="Card"/> objects.
    /// </returns>
    public static async Task<List<Card>> GetRandomCards(int count, string setId)
    {
        var random = new Random();
        const int pageSize = 250;
        int randomPage = random.Next(1, 10);

        string requestUrl = setId == null
            ? $"{ApiUrl}?page={randomPage}&pageSize={pageSize}"
            : $"{ApiUrl}?q=set.id%3A{setId}&pageSize={pageSize}";

        try
        {
            var stopwatch = Stopwatch.StartNew();
            string response = await _httpClient.GetStringAsync(requestUrl);
            stopwatch.Stop();
            LastApiLatency = stopwatch.ElapsedMilliseconds;
            Console.WriteLine($"API Latency: {LastApiLatency}ms");
            var cardData = JsonConvert.DeserializeObject<ApiResponse>(response);
            return cardData?.Data?.OrderBy(_ => random.Next()).Take(count).ToList() ?? new List<Card>();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to get random cards, ", ex.Message);
            return [];
        }
    }

    /// <summary>
    ///     Handles cleanup when a user leaves the guild by deleting their saved card collection.
    /// </summary>
    /// <param name="guild">
    ///     The guild from which the user left.
    /// </param>
    /// <param name="user">
    ///     The user who left.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static Task HandleUserLeft(SocketGuild guild, SocketUser user)
    {
        string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{user.Id}.json");

        if (File.Exists(userFilePath))
        {
            File.Delete(userFilePath);
            Console.WriteLine($"Deleted JSON file for user {user.Username} ({user.Id}).");
        }
        else
        {
            Console.WriteLine($"No JSON file found for user {user.Username} ({user.Id}).");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Clears the last trade session
    /// </summary>
    public static void ClearTradeSessions()
    {
        ActiveTrades.Clear();
        Console.WriteLine("Trade sessions cleared.");
    }
}