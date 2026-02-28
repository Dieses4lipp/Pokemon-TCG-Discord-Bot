namespace DiscordBot.Models;

/// <summary>
///     Represents a Pokémon card with its name, rarity, and images.
/// </summary>
public class Card
{
    /// <summary>
    ///     Gets or sets the name of the card.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the rarity of the card.
    /// </summary>
    public string Rarity { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the image urls without "/low.png" or "/high.png" suffix.
    /// </summary>
    public string Image { get; set; } = default!;
}