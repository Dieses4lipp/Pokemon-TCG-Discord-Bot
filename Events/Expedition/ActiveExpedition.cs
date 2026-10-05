using DiscordBot.Models;

namespace DiscordBot.Events.Expedtion;

/// <summary>
///     Represents an expedition currently in progress for a user.
/// </summary>
public class ActiveExpedition
{
    /// <summary>
    ///     Gets or sets the id of the location (see <see cref="ExpeditionLocation.Id"/>) the
    ///     expedition was sent to.
    /// </summary>
    public string LocationId { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the UTC time the expedition was started.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    ///     Gets or sets the UTC time the expedition finishes and can be claimed.
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    ///     Gets or sets the cards sent on the expedition (locked for the duration). Matched back
    ///     to the user's collection by Name + Rarity.
    /// </summary>
    public List<Card> SentCards { get; set; } = [];
}
