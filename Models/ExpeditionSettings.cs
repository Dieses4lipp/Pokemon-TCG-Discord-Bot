namespace DiscordBot.Models;

/// <summary>
///     Represents the full set of configured expedition locations, loaded from the expedition
///     settings JSON file.
/// </summary>
public class ExpeditionSettings
{
    /// <summary>
    ///     Gets or sets the list of locations a user can send an expedition to.
    /// </summary>
    public List<ExpeditionLocation> Locations { get; set; } = [];
}

/// <summary>
///     Represents a single expedition location, its duration, and its possible loot.
/// </summary>
public class ExpeditionLocation
{
    /// <summary>
    ///     Gets or sets the unique identifier of the location (used to reference it in commands).
    /// </summary>
    public string Id { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the display name of the location.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    ///     Gets or sets how long an expedition to this location takes, in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    ///     Gets or sets the minimum coin reward awarded on claim.
    /// </summary>
    public double MinReward { get; set; }

    /// <summary>
    ///     Gets or sets the maximum coin reward awarded on claim.
    /// </summary>
    public double MaxReward { get; set; }

    /// <summary>
    ///     Gets or sets the chance (0.0-1.0) that a card is awarded in addition to coins on claim.
    /// </summary>
    public double CardRewardChance { get; set; }

    /// <summary>
    ///     Gets or sets how many cards are awarded when the card reward triggers.
    /// </summary>
    public int CardRewardCount { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the set id to draw the reward card(s) from via
    ///     <c>CommandHandler.GetRandomCards</c>. Empty/null means no restriction is applied by
    ///     this location (a set id still must be supplied at claim time).
    /// </summary>
    public string? CardRewardSetId { get; set; }
}
