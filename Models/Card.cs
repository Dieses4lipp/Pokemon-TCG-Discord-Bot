using Newtonsoft.Json;

namespace DiscordBot.Models;

/// <summary>
///     Represents a Pokémon card with its name, rarity, and images.
/// </summary>
public record Card
{
    private string _setId = default!;
    [JsonProperty("id")]
    public string SetId
    {
        get => _setId;
        set
        {
            if (!string.IsNullOrEmpty(value) && value.Contains('-'))
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
    public string? Image { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the card is locked.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>
    ///     Gets or sets the pricing information for the card.
    /// </summary>
    [JsonProperty("pricing")]
    public CardPricing? Pricing { get; set; }
}

/// <summary>
///     Represents pricing information for a card from various marketplaces.
/// </summary>
public record CardPricing
{
    /// <summary>
    ///     Gets or sets the Cardmarket pricing information.
    /// </summary>
    [JsonProperty("cardmarket")]
    public CardmarketPricing? Cardmarket { get; set; }

    /// <summary>
    ///     Gets or sets the TCGPlayer pricing information.
    /// </summary>
    [JsonProperty("tcgplayer")]
    public TcgPlayerPricing? TcgPlayer { get; set; }
}

/// <summary>
///     Represents pricing information from Cardmarket.
/// </summary>
public record CardmarketPricing
{
    /// <summary>
    ///     Gets or sets the currency unit (e.g., "EUR").
    /// </summary>
    [JsonProperty("unit")]
    public string? Unit { get; set; }

    /// <summary>
    ///     Gets or sets the average price.
    /// </summary>
    [JsonProperty("avg")]
    public double? Avg { get; set; }

    /// <summary>
    ///     Gets or sets the lowest price.
    /// </summary>
    [JsonProperty("low")]
    public double? Low { get; set; }

    /// <summary>
    ///     Gets or sets the current price trend.
    /// </summary>
    [JsonProperty("trend")]
    public double? Trend { get; set; }
}

/// <summary>
///     Represents pricing information from TCGPlayer.
/// </summary>
public record TcgPlayerPricing
{
    /// <summary>
    ///     Gets or sets the currency unit (e.g., "USD").
    /// </summary>
    [JsonProperty("unit")]
    public string? Unit { get; set; }

    /// <summary>
    ///     Gets or sets the market price.
    /// </summary>
    [JsonProperty("market")]
    public double? Market { get; set; }

    /// <summary>
    ///     Gets or sets the lowest price.
    /// </summary>
    [JsonProperty("low")]
    public double? Low { get; set; }

    /// <summary>
    ///     Gets or sets the high price.
    /// </summary>
    [JsonProperty("high")]
    public double? High { get; set; }
}