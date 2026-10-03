namespace DiscordBot.Models;

/// <summary>
///     Represents one or more card positions in a pack that share the same rarity odds.
/// </summary>
public class PackSlot
{
    /// <summary>
    ///     The rarity key that matches any card of the set.
    /// </summary>
    public const string AnyRarity = "*";

    /// <summary>
    ///     Gets or sets the display name of the slot.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets how many cards are drawn with this slot's odds.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    ///     Gets or sets the chance in percent (summing to 100) for each rarity, keyed by the
    ///     rarity name as delivered by the card API, or <see cref="AnyRarity"/> for any card.
    /// </summary>
    public Dictionary<string, double> Rarities { get; set; } = [];
}
