using Newtonsoft.Json;

namespace DiscordBot.Models;

/// <summary>
///     Represents a Pokémon card with its name, rarity, and images.
/// </summary>
public class Card
{
    private string _setId = default!;
    [JsonProperty("id")]
    public string SetId
    {
        get => _setId;
        set
        {
            if(!string.IsNullOrEmpty(value) && value.Contains('-'))
            {
                _setId = value.Split('-')[0];
            }
            else
            {
                _setId = value;
            }
        }
    }
    [JsonProperty("localId")]
    public string LocalId { get; set; } = default!;
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