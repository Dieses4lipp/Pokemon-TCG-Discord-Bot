namespace DiscordBot.Models;

/// <summary>
///     Represents the API response containing a list of Pokémon card sets.
/// </summary>
public class SetApiResponse
{
    /// <summary>
    ///     Gets or sets the list of sets returned from the API.
    /// </summary>
    public List<Set> Data { get; set; } = [];
}