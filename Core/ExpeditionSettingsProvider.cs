using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Loads and provides access to the configured expedition locations from the expedition
///     settings JSON file.
/// </summary>
public static class ExpeditionSettingsProvider
{
    private static readonly string SettingsFilePath = Path.Combine(
        AppContext.BaseDirectory,
        "Data",
        "expeditionSettings.json"
    );

    private static ExpeditionSettings? _settings;

    /// <summary>
    ///     Gets the configured expedition locations, loading them from disk on first access.
    /// </summary>
    public static IReadOnlyList<ExpeditionLocation> Locations => GetSettings().Locations;

    /// <summary>
    ///     Finds a configured expedition location by its id (case-insensitive).
    /// </summary>
    /// <param name="id">
    ///     The location id to look for.
    /// </param>
    /// <returns>
    ///     The matching <see cref="ExpeditionLocation"/>, or <see langword="null"/> if none found.
    /// </returns>
    public static ExpeditionLocation? GetLocationById(string id)
    {
        return GetSettings().Locations
            .FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static ExpeditionSettings GetSettings()
    {
        if (_settings != null) return _settings;

        if (!File.Exists(SettingsFilePath))
        {
            _settings = new ExpeditionSettings();
            return _settings;
        }

        var json = File.ReadAllText(SettingsFilePath);
        _settings = JsonConvert.DeserializeObject<ExpeditionSettings>(json) ?? new ExpeditionSettings();
        return _settings;
    }
}
