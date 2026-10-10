using DiscordBot.Models;
using Microsoft.Data.Sqlite;
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
        await using (var connection = await database.OpenAsync())
        {
            if (await SqliteCommands.ScalarAsync(connection, null, "SELECT COUNT(*) FROM expedition_locations;") is 0L)
            {
                var seed = ReadSeedFile();
                await using var transaction = connection.BeginTransaction();
                await ReplaceAllAsync(connection, transaction, seed);
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

        await using (var connection = await database.OpenAsync())
        await using (var transaction = connection.BeginTransaction())
        {
            await ReplaceAllAsync(connection, transaction, seed);
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

        await using (var connection = await database.OpenAsync())
        {
            await UpsertAsync(connection, null, location);
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var locations = new List<ExpeditionLocation>();

        await using var connection = await database.OpenAsync();
        await using var command = SqliteCommands.Create(connection, null, """
            SELECT id, name, duration_minutes, min_reward, max_reward, card_reward_chance, card_reward_count, card_reward_set_id
            FROM expedition_locations ORDER BY id;
            """);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            locations.Add(new ExpeditionLocation
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                DurationMinutes = reader.GetInt32(2),
                MinReward = reader.GetDouble(3),
                MaxReward = reader.GetDouble(4),
                CardRewardChance = reader.GetDouble(5),
                CardRewardCount = reader.GetInt32(6),
                CardRewardSetId = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }

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

    private static async Task ReplaceAllAsync(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<ExpeditionLocation> locations)
    {
        await SqliteCommands.ExecuteAsync(connection, transaction, "DELETE FROM expedition_locations;");
        foreach (var location in locations)
            await UpsertAsync(connection, transaction, location);
    }

    private static Task<int> UpsertAsync(SqliteConnection connection, SqliteTransaction? transaction, ExpeditionLocation location) =>
        SqliteCommands.ExecuteAsync(connection, transaction, """
            INSERT INTO expedition_locations (id, name, duration_minutes, min_reward, max_reward, card_reward_chance, card_reward_count, card_reward_set_id)
            VALUES ($id, $name, $durationMinutes, $minReward, $maxReward, $cardRewardChance, $cardRewardCount, $cardRewardSetId)
            ON CONFLICT (id) DO UPDATE SET
                name = excluded.name,
                duration_minutes = excluded.duration_minutes,
                min_reward = excluded.min_reward,
                max_reward = excluded.max_reward,
                card_reward_chance = excluded.card_reward_chance,
                card_reward_count = excluded.card_reward_count,
                card_reward_set_id = excluded.card_reward_set_id;
            """,
            ("$id", location.Id),
            ("$name", location.Name),
            ("$durationMinutes", location.DurationMinutes),
            ("$minReward", location.MinReward),
            ("$maxReward", location.MaxReward),
            ("$cardRewardChance", location.CardRewardChance),
            ("$cardRewardCount", location.CardRewardCount),
            ("$cardRewardSetId", string.IsNullOrWhiteSpace(location.CardRewardSetId) ? null : location.CardRewardSetId));
}
