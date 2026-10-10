using DiscordBot.Events.Expedition;

namespace DiscordBot.Models.Legacy;

/// <summary>
///     A user collection as stored in <c>UserCards/&lt;userId&gt;.json</c> before the SQLite database.
/// </summary>
public class LegacyUserCardCollection
{
    public ulong UserId { get; set; }

    public List<Card> Cards { get; set; } = [];

    public int PacksPulled { get; set; }

    public int CardsTraded { get; set; }

    public decimal Balance { get; set; }

    /// <summary>
    ///     Gets or sets a copy of the favorite card, matched back to the collection by name and rarity.
    /// </summary>
    public Card? FavoriteCard { get; set; }

    /// <summary>
    ///     Gets or sets the running expedition; its old <c>SentCards</c> list is ignored, the sent
    ///     cards are the locked ones.
    /// </summary>
    public ActiveExpedition? ActiveExpedition { get; set; }
}
