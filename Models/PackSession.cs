using DiscordBot.Services;

namespace DiscordBot.Models;

/// <summary>
///     Represents a session for navigating through a pack of Pokémon cards.
/// </summary>
public class PackSession(ulong messageId, ulong userId, List<Card> cards)
{
    /// <summary>
    ///     Gets the message ID associated with the session.
    /// </summary>
    public ulong MessageId { get; } = messageId;

    /// <summary>
    ///     Gets the user ID of the session participant.
    /// </summary>
    public ulong UserId { get; } = userId;

    /// <summary>
    ///     Gets the list of cards in the session.
    /// </summary>
    public List<Card> Cards { get; } = cards;

    /// <summary>
    ///     Gets or sets the current index of the displayed card.
    /// </summary>
    public int CurrentIndex { get; set; }

    /// <summary>
    ///     A set of identifiers (for example, a combination of card name and rarity) representing
    ///     cards in this pack that have already been saved.
    /// </summary>
    public HashSet<string> SavedCardIdentifiers { get; set; } = [];
}