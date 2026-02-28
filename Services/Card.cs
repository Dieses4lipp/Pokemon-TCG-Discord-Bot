using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Services;

/// <summary>
///     Represents a Pokémon card with its name, rarity, and images.
/// </summary>
public class Card
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;

    [JsonProperty("name")]
    public string Name { get; set; } = default!;

    [JsonProperty("rarity")]
    public string Rarity { get; set; } = "Unknown";

    [JsonProperty("images")]
    public CardImages Images { get; set; } = new CardImages();

    // Backwards-compatible accessors used by the rest of the bot
    public CardImages DisplayImages => Images ?? new CardImages();
}

public class CardImages
{
    [JsonProperty("small")]
    public string Small { get; set; } = string.Empty;

    [JsonProperty("large")]
    public string Large { get; set; } = string.Empty;
}

public class CardBrief
{
    [JsonProperty("id")]
    public string Id { get; set; } = default!;
}

/// <summary>
/// Response wrapper for a set endpoint that returns a list of card ids.
/// The property name varies between APIs; parse code will attempt to find plausible arrays.
/// </summary>
public class TcgDexSetResponse
{
    [JsonProperty("cards")]
    public List<CardBrief> Cards { get; set; } = new();
}