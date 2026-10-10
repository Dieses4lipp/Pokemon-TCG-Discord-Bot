using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     The constraints an expedition location has to meet, checked for the shipped settings file
///     and for every admin edit.
/// </summary>
public static class ExpeditionLocationRules
{
    /// <returns>
    ///     Why the location is invalid, or <see langword="null"/> if it is valid.
    /// </returns>
    public static string? Validate(ExpeditionLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.Id)) return "the ID must not be empty";
        if (string.IsNullOrWhiteSpace(location.Name)) return "the name must not be empty";
        if (location.DurationMinutes <= 0) return "the duration must be at least 1 minute";
        if (location.MinReward < 0) return "the minimum reward must not be negative";
        if (location.MaxReward < location.MinReward) return "the maximum reward must not be below the minimum reward";
        if (location.CardRewardChance is < 0 or > 1) return "the card reward chance must be between 0 and 1";
        if (location.CardRewardCount < 1) return "the card reward count must be at least 1";
        return null;
    }
}
