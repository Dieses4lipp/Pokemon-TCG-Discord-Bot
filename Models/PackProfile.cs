namespace DiscordBot.Models;

/// <summary>
///     Represents the layout of a booster pack: its price and the slots its cards are drawn from.
/// </summary>
public class PackProfile
{
    /// <summary>
    ///     Gets or sets a human readable description of the pack layout.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the number of cards in a pack. Must equal the sum of all slot counts.
    /// </summary>
    public int CardsPerPack { get; set; }

    /// <summary>
    ///     Gets or sets the price of one pack.
    /// </summary>
    public double PackPrice { get; set; }

    /// <summary>
    ///     Gets or sets the slots of the pack, in the order the cards are revealed.
    /// </summary>
    public List<PackSlot> Slots { get; set; } = [];
}
