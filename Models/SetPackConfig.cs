namespace DiscordBot.Models;

/// <summary>
///     Represents the pack configuration of a single set.
/// </summary>
public class SetPackConfig
{
    /// <summary>
    ///     Gets or sets the name of the <see cref="PackProfile"/> this set's packs use.
    /// </summary>
    public string Profile { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the cover image file names inside the set's cover folder. One is picked at
    ///     random per pack; empty means the default cover is used.
    /// </summary>
    public List<string> Covers { get; set; } = [];
}
