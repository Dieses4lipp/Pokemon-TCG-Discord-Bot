using DiscordBot.Events.Expedition;

namespace DiscordBot.Models;

/// <summary>
///     Represents a collection of Pokémon cards for a specific user.
/// </summary>
public class UserCardCollection
{
    /// <summary>
    ///     Gets or sets the ID of the user associated with the card collection.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    ///     Gets or sets the list of cards owned by the user.
    /// </summary>
    public List<Card> Cards { get; set; } = [];

    /// <summary>
    ///     Gets or sets the number of packs the user has pulled.
    /// </summary>
    public int PacksPulled { get; set; }

    /// <summary>
    ///     Gets or sets the number of cards the user has traded.
    /// </summary>
    public int CardsTraded { get; set; }

    /// <summary>
    ///     Gets the number of distinct different cards the user has saved.
    /// </summary>
    public int DifferentCardsSaved => Cards.Select(c => c.Name).Distinct().Count();

    /// <summary>
    ///     Gets or sets the user's current balance Balance earned from selling cards.
    /// </summary>
    public decimal Balance { get; set; }


    /// <summary>
    ///     Gets or sets the <see cref="Card.InstanceId"/> of the user's favorite copy, if any.
    /// </summary>
    public long? FavoriteCardId { get; set; }

    /// <summary>
    ///     Gets the user's favorite card; <see langword="null"/> once that copy left the collection.
    /// </summary>
    public Card? FavoriteCard => FavoriteCardId is { } id ? Cards.FirstOrDefault(c => c.InstanceId == id) : null;

    /// <summary>
    ///     Gets or sets the active expedition for the user.
    /// </summary>
    public ActiveExpedition? ActiveExpedition { get; set; }

    /// <summary>
    ///     Gets the cards away on the expedition; a card is locked exactly while it is away.
    /// </summary>
    public IEnumerable<Card> CardsOnExpedition => Cards.Where(c => c.IsLocked);

}