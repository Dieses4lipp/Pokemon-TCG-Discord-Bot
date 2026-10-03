using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Loads, validates and provides access to the booster pack configuration from the pack
///     settings JSON file.
/// </summary>
public static class PackSettingsProvider
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "packSettings.json"
    );

    private static PackSettings? _settings;

    /// <summary>
    ///     Gets the pack profile used for a set, falling back to the default profile for sets
    ///     that are not configured.
    /// </summary>
    /// <param name="setId">
    ///     The set ID to look up.
    /// </param>
    /// <returns>
    ///     The <see cref="PackProfile"/> for the set.
    /// </returns>
    public static PackProfile GetProfile(string setId)
    {
        var settings = GetSettings();
        string profileName = settings.Sets.TryGetValue(setId, out var set) ? set.Profile : settings.DefaultProfile;
        return settings.Profiles[profileName];
    }

    /// <summary>
    ///     Picks a random cover image for a set, falling back to the default cover if the set has
    ///     no configured cover or the file is missing.
    /// </summary>
    /// <param name="setId">
    ///     The set ID to pick a cover for.
    /// </param>
    /// <param name="random">
    ///     The random number generator used to pick a cover.
    /// </param>
    /// <returns>
    ///     The absolute path of the cover image.
    /// </returns>
    public static string GetRandomCoverPath(string setId, Random random)
    {
        var settings = GetSettings();
        string coversRoot = Path.Combine(AppContext.BaseDirectory, settings.CoversDirectory);

        if (settings.Sets.TryGetValue(setId, out var set) && set.Covers.Count > 0)
        {
            string coverPath = Path.Combine(coversRoot, setId, set.Covers[random.Next(set.Covers.Count)]);
            if (File.Exists(coverPath)) return coverPath;

            Console.WriteLine($"Pack cover '{coverPath}' is configured but missing, using default cover.");
        }

        return Path.Combine(coversRoot, settings.DefaultCover);
    }

    private static PackSettings GetSettings()
    {
        if (_settings != null) return _settings;

        if (!File.Exists(SettingsFilePath))
        {
            throw new FileNotFoundException(
                $"Pack settings file not found at '{SettingsFilePath}'.", SettingsFilePath);
        }

        var json = File.ReadAllText(SettingsFilePath);
        var settings = JsonConvert.DeserializeObject<PackSettings>(json) ?? new PackSettings();

        var errors = Validate(settings);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid pack settings in '{SettingsFilePath}':\n- {string.Join("\n- ", errors)}");
        }

        _settings = settings;
        return _settings;
    }

    /// <summary>
    ///     Checks the pack settings for inconsistencies, so a broken config fails loudly instead of
    ///     producing wrong packs.
    /// </summary>
    /// <param name="settings">
    ///     The settings to check.
    /// </param>
    /// <returns>
    ///     A list of error messages; empty if the settings are valid.
    /// </returns>
    private static List<string> Validate(PackSettings settings)
    {
        var errors = new List<string>();

        if (!settings.Profiles.ContainsKey(settings.DefaultProfile ?? string.Empty))
            errors.Add($"defaultProfile '{settings.DefaultProfile}' is not a defined profile.");

        foreach (var (name, profile) in settings.Profiles)
        {
            if (profile.Slots.Count == 0)
                errors.Add($"Profile '{name}' has no slots.");

            int slotCardCount = profile.Slots.Sum(s => s.Count);
            if (slotCardCount != profile.CardsPerPack)
                errors.Add($"Profile '{name}': cardsPerPack is {profile.CardsPerPack} but its slots add up to {slotCardCount}.");

            if (profile.PackPrice < 0)
                errors.Add($"Profile '{name}': packPrice must not be negative.");

            foreach (var slot in profile.Slots)
            {
                if (slot.Count <= 0)
                    errors.Add($"Profile '{name}', slot '{slot.Name}': count must be positive.");

                if (slot.Rarities.Values.Any(p => p <= 0))
                    errors.Add($"Profile '{name}', slot '{slot.Name}': every rarity chance must be positive.");

                double total = slot.Rarities.Values.Sum();
                if (Math.Abs(total - 100) > 0.01)
                    errors.Add($"Profile '{name}', slot '{slot.Name}': rarity chances add up to {total}%, expected 100%.");
            }
        }

        foreach (var (setId, set) in settings.Sets)
        {
            if (!settings.Profiles.ContainsKey(set.Profile ?? string.Empty))
                errors.Add($"Set '{setId}' uses unknown profile '{set.Profile}'.");
        }

        return errors;
    }
}
