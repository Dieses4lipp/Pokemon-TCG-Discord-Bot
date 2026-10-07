using DiscordBot.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Loads, validates and provides access to the booster pack configuration from the pack
///     settings JSON file.
/// </summary>
public sealed class PackSettingsProvider(ILogger<PackSettingsProvider> logger)
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "packSettings.json"
    );

    private PackSettings? _settings;

    /// <summary>
    ///     Gets the pack profile used for a set, falling back to the default profile for sets
    ///     that are not configured.
    /// </summary>
    /// <param name="setId">
    ///     The set ID to look up, in any casing.
    /// </param>
    /// <returns>
    ///     The <see cref="PackProfile"/> for the set.
    /// </returns>
    public PackProfile GetProfile(string setId)
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
    ///     The set ID to pick a cover for, in any casing.
    /// </param>
    /// <param name="random">
    ///     The random number generator used to pick a cover.
    /// </param>
    /// <returns>
    ///     The absolute path of the cover image.
    /// </returns>
    public string GetRandomCoverPath(string setId, Random random)
    {
        var settings = GetSettings();
        string coversRoot = Path.Combine(AppContext.BaseDirectory, settings.CoversDirectory);

        if (settings.Sets.TryGetValue(setId, out var set) && set.Covers.Count > 0)
        {
            string coverPath = Path.Combine(coversRoot, set.Folder, set.Covers[random.Next(set.Covers.Count)]);
            if (File.Exists(coverPath)) return coverPath;

            logger.LogWarning("Pack cover '{CoverPath}' is configured but missing, using default cover", coverPath);
        }

        return Path.Combine(coversRoot, settings.DefaultCover);
    }

    private PackSettings GetSettings()
    {
        if (_settings != null) return _settings;

        if (!File.Exists(SettingsFilePath))
        {
            throw new FileNotFoundException(
                $"Pack settings file not found at '{SettingsFilePath}'.", SettingsFilePath);
        }

        _settings = Parse(File.ReadAllText(SettingsFilePath), SettingsFilePath);
        return _settings;
    }

    /// <summary>
    ///     Parses and validates pack settings JSON. Set IDs become case-insensitive, because the
    ///     card API reports some of them in upper case (e.g. "A1") while the cover folders and
    ///     config keys are lower case.
    /// </summary>
    /// <param name="json">
    ///     The pack settings JSON.
    /// </param>
    /// <param name="source">
    ///     Where the JSON came from, used in the error message.
    /// </param>
    /// <returns>
    ///     The validated settings.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     The settings are inconsistent.
    /// </exception>
    internal static PackSettings Parse(string json, string source)
    {
        var settings = JsonConvert.DeserializeObject<PackSettings>(json) ?? new PackSettings();
        var errors = new List<string>();

        var sets = new Dictionary<string, SetPackConfig>(StringComparer.OrdinalIgnoreCase);
        foreach (var (setId, set) in settings.Sets)
        {
            set.Folder = setId;
            if (!sets.TryAdd(setId, set))
                errors.Add($"Set '{setId}' is configured more than once (set IDs are case-insensitive).");
        }
        settings.Sets = sets;

        errors.AddRange(Validate(settings));
        if (errors.Count > 0)
        {
            throw new InvalidDataException(
                $"Invalid pack settings in '{source}':\n- {string.Join("\n- ", errors)}");
        }

        return settings;
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
    internal static List<string> Validate(PackSettings settings)
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
