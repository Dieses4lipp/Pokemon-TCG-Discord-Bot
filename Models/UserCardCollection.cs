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
    ///     Gets or sets the user's favorite card, if any.
    /// </summary>
    public Card? FavoriteCard { get; set; }
}