namespace DiscordBot.Models;

/// <summary>
///     Represents the central booster pack configuration: pack layouts (profiles) and, per set,
///     which profile and cover images are used. Loaded from the pack settings JSON file.
/// </summary>
public class PackSettings
{
    /// <summary>
    ///     Gets or sets the directory containing one cover folder per set, relative to the app directory.
    /// </summary>
    public string CoversDirectory { get; set; } = "Assets/sets_covers";

    /// <summary>
    ///     Gets or sets the cover image (inside <see cref="CoversDirectory"/>) used when a set has none.
    /// </summary>
    public string DefaultCover { get; set; } = "default.jpg";

    /// <summary>
    ///     Gets or sets the profile used for sets that are not listed in <see cref="Sets"/>.
    /// </summary>
    public string DefaultProfile { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the pack layouts, keyed by profile name.
    /// </summary>
    public Dictionary<string, PackProfile> Profiles { get; set; } = [];

    /// <summary>
    ///     Gets or sets the per-set configuration, keyed by set ID.
    /// </summary>
    public Dictionary<string, SetPackConfig> Sets { get; set; } = [];
}
