namespace DiscordBot.Models.Legacy;

/// <summary>
///     A paid pack session as stored in <c>UserCards/State/botState.json</c>, where saved cards
///     were tracked by name and rarity instead of position.
/// </summary>
public class LegacyPackSession
{
    public ulong MessageId { get; set; }

    public ulong UserId { get; set; }

    public List<Card> Cards { get; set; } = [];

    public int CurrentIndex { get; set; }

    /// <summary>
    ///     Gets or sets the saved cards as <c>"{Name}_{Rarity}"</c>.
    /// </summary>
    public HashSet<string> SavedCardIdentifiers { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}
