using DiscordBot.Core.Storage;
using DiscordBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     The expedition locations, kept in the database so admins can change them without an image
///     rebuild. <c>Data/expeditionSettings.json</c> ships the defaults: it fills an empty table and
///     is what <see cref="ReloadFromFileAsync"/> resets to. Reads come from an in-memory copy.
/// </summary>
public sealed class ExpeditionLocationStore(BotDatabase database, string seedFilePath, ILogger<ExpeditionLocationStore> logger)
{
    /// <summary>
    ///     The shipped defaults, next to the binary.
    /// </summary>
    public static readonly string DefaultSeedFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "expeditionSettings.json");

    private IReadOnlyList<ExpeditionLocation> _locations = [];

    /// <summary>
    ///     Gets the locations, ordered by ID.
    /// </summary>
    public IReadOnlyList<ExpeditionLocation> Locations => Volatile.Read(ref _locations);

    /// <summary>
    ///     Finds a location by its ID (case-insensitive).
    /// </summary>
    /// <returns>
    ///     The location, or <see langword="null"/> if there is none with that ID.
    /// </returns>
    public ExpeditionLocation? GetLocationById(string id) =>
        Locations.FirstOrDefault(l => l.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Loads the locations, filling the table from the settings file on the first start.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task LoadAsync()
    {
        await using (var db = database.CreateContext())
        {
            if (!await db.ExpeditionLocations.AnyAsync())
            {
                var seed = ReadSeedFile();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await ReplaceAllAsync(db, seed);
                await transaction.CommitAsync();
                logger.LogInformation("Filled the expedition locations from {SeedFile} ({Count} locations)", seedFilePath, seed.Count);
            }
        }

        await RefreshAsync();
    }

    /// <summary>
    ///     Replaces every location with the ones in the settings file, undoing all edits.
    /// </summary>
    /// <returns>
    ///     The number of locations loaded.
    /// </returns>
    public async Task<int> ReloadFromFileAsync()
    {
        var seed = ReadSeedFile();

        await using (var db = database.CreateContext())
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await ReplaceAllAsync(db, seed);
            await transaction.CommitAsync();
        }

        await RefreshAsync();
        return seed.Count;
    }

    /// <summary>
    ///     Saves a location, adding it if its ID is new.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The location breaks a rule in <see cref="ExpeditionLocationRules"/>.
    /// </exception>
    public async Task SaveAsync(ExpeditionLocation location)
    {
        if (ExpeditionLocationRules.Validate(location) is { } error)
            throw new ArgumentException($"Expedition location '{location.Id}' is invalid: {error}.", nameof(location));

        await using (var db = database.CreateContext())
        {
            // The ID is matched case-insensitively, as admins type it; the stored spelling stays
            var existing = (await db.ExpeditionLocations.ToListAsync())
                .FirstOrDefault(l => l.Id.Equals(location.Id, StringComparison.OrdinalIgnoreCase));
            var stored = ToStored(location);
            if (existing == null)
            {
                db.ExpeditionLocations.Add(stored);
            }
            else
            {
                stored.Id = existing.Id;
                db.Entry(existing).CurrentValues.SetValues(stored);
            }
            await db.SaveChangesAsync();
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await using var db = database.CreateContext();
        List<ExpeditionLocation> locations = await db.ExpeditionLocations.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
        Volatile.Write(ref _locations, locations);
    }

    /// <exception cref="InvalidDataException">
    ///     The file is missing, unreadable, has a duplicate ID or an invalid location.
    /// </exception>
    private List<ExpeditionLocation> ReadSeedFile()
    {
        if (!File.Exists(seedFilePath))
            throw new InvalidDataException($"Expedition settings file not found at '{seedFilePath}'.");

        var locations = JsonConvert.DeserializeObject<ExpeditionSettings>(File.ReadAllText(seedFilePath))?.Locations ?? [];

        foreach (var location in locations)
        {
            if (ExpeditionLocationRules.Validate(location) is { } error)
                throw new InvalidDataException($"Expedition location '{location.Id}' in '{seedFilePath}' is invalid: {error}.");
        }

        var duplicate = locations.GroupBy(l => l.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
            throw new InvalidDataException($"Expedition location ID '{duplicate.Key}' appears more than once in '{seedFilePath}'.");

        return locations;
    }

    private static async Task ReplaceAllAsync(BotDbContext db, IEnumerable<ExpeditionLocation> locations)
    {
        await db.ExpeditionLocations.ExecuteDeleteAsync();
        db.ExpeditionLocations.AddRange(locations.Select(ToStored));
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     A copy to hand to the database, so the context never tracks an object callers still hold.
    ///     An empty reward set means none.
    /// </summary>
    private static ExpeditionLocation ToStored(ExpeditionLocation location) => new()
    {
        Id = location.Id,
        Name = location.Name,
        DurationMinutes = location.DurationMinutes,
        MinReward = location.MinReward,
        MaxReward = location.MaxReward,
        CardRewardChance = location.CardRewardChance,
        CardRewardCount = location.CardRewardCount,
        CardRewardSetId = string.IsNullOrWhiteSpace(location.CardRewardSetId) ? null : location.CardRewardSetId,
    };
}
