using System.Globalization;
using DiscordBot.Models;
using DiscordBot.Models.Legacy;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Moves the JSON files the bot kept before the SQLite database (<c>UserCards/*.json</c>,
///     <c>UserCards/Left/*.json</c>, <c>UserCards/State/botState.json</c>) into the database, once.
///     The files are only read, never changed, so they stay a backup.
/// </summary>
public sealed class LegacyJsonImporter(BotDatabase database, ILogger<LegacyJsonImporter> logger)
{
    /// <summary>
    ///     Where the JSON files were kept: <c>/app/UserCards</c> in the container.
    /// </summary>
    public static readonly string DefaultDirectory = Path.Combine(AppContext.BaseDirectory, "UserCards");

    private const string ImportedAtKey = "legacy_json_imported_at";

    /// <summary>
    ///     Imports the JSON files in one transaction, unless an earlier run already did. Users the
    ///     database already knows are skipped, so data written since is never overwritten. Throws
    ///     on an unreadable file instead of skipping it, so no collection is lost silently.
    /// </summary>
    /// <param name="directory">
    ///     The old <c>UserCards</c> directory.
    /// </param>
    /// <returns>
    ///     What was imported, or <see langword="null"/> if there was nothing to do.
    /// </returns>
    public async Task<LegacyImportResult?> ImportAsync(string directory)
    {
        await using var connection = await database.OpenAsync();

        if (await SqliteCommands.ScalarAsync(connection, null,
                "SELECT value FROM settings WHERE key = $key;", ("$key", ImportedAtKey)) is string importedAt)
        {
            logger.LogDebug("Legacy JSON files were imported at {ImportedAt}, skipping", importedAt);
            return null;
        }

        // Not marked as done: if the volume is only missing, the import still runs once it is mounted
        if (!Directory.Exists(directory))
        {
            logger.LogInformation("No legacy JSON directory at {Directory}, nothing to import", directory);
            return null;
        }

        await using var transaction = connection.BeginTransaction();

        int users = 0, archivedUsers = 0, skippedUsers = 0, cards = 0;

        var files = Directory.GetFiles(directory, "*.json").Select(path => (Path: path, Archived: false));
        string archiveDirectory = Path.Combine(directory, "Left");
        if (Directory.Exists(archiveDirectory))
            files = files.Concat(Directory.GetFiles(archiveDirectory, "*.json").Select(path => (Path: path, Archived: true)));

        foreach (var (path, archived) in files.ToList())
        {
            var legacy = ReadJson<LegacyUserCardCollection>(path);
            ulong userId = legacy.UserId != 0
                ? legacy.UserId
                : ulong.Parse(Path.GetFileNameWithoutExtension(path), CultureInfo.InvariantCulture);

            if (await SqliteCommands.ScalarAsync(connection, transaction,
                    "SELECT 1 FROM users WHERE user_id = $userId;", ("$userId", (long)userId)) != null)
            {
                logger.LogWarning("User {UserId} already exists in the database, skipping {File}", userId, path);
                skippedUsers++;
                continue;
            }

            var collection = ToCollection(userId, legacy);
            await UserRepository.SaveAsync(connection, transaction, [collection]);

            // The favorite was a copy of the card; point it at the first copy with its name and rarity
            var favorite = legacy.FavoriteCard == null
                ? null
                : collection.Cards.FirstOrDefault(c => c.Name == legacy.FavoriteCard.Name && c.Rarity == legacy.FavoriteCard.Rarity);
            if (favorite != null)
            {
                collection.FavoriteCardId = favorite.InstanceId;
                await UserRepository.SaveAsync(connection, transaction, [collection]);
            }

            if (archived)
            {
                // The archive time was stamped on the file when the user left
                await SqliteCommands.ExecuteAsync(connection, transaction,
                    "UPDATE users SET left_at_utc = $leftAt WHERE user_id = $userId;",
                    ("$userId", (long)userId),
                    ("$leftAt", File.GetLastWriteTimeUtc(path).ToString("O", CultureInfo.InvariantCulture)));
                archivedUsers++;
            }

            users++;
            cards += collection.Cards.Count;
        }

        bool botStateImported = await ImportBotStateAsync(connection, transaction, Path.Combine(directory, "State", "botState.json"));

        await SqliteCommands.ExecuteAsync(connection, transaction,
            "INSERT INTO settings (key, value) VALUES ($key, $value);",
            ("$key", ImportedAtKey), ("$value", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

        await transaction.CommitAsync();

        var result = new LegacyImportResult(users, archivedUsers, skippedUsers, cards, botStateImported);
        logger.LogInformation(
            "Imported legacy JSON files from {Directory}: {Users} users ({ArchivedUsers} archived, {SkippedUsers} skipped), {Cards} cards, bot state {BotStateImported}",
            directory, result.Users, result.ArchivedUsers, result.SkippedUsers, result.Cards, result.BotStateImported ? "imported" : "not found");
        return result;
    }

    private static UserCardCollection ToCollection(ulong userId, LegacyUserCardCollection legacy)
    {
        var collection = new UserCardCollection
        {
            UserId = userId,
            Balance = Math.Round(legacy.Balance, 2, MidpointRounding.AwayFromZero),
            PacksPulled = legacy.PacksPulled,
            CardsTraded = legacy.CardsTraded,
            ActiveExpedition = legacy.ActiveExpedition,
            Cards = legacy.Cards.Select(card => card with { InstanceId = 0 }).ToList(),
        };

        // Locked means away on an expedition; a lock without one would keep the card stuck forever
        if (collection.ActiveExpedition == null)
        {
            foreach (var card in collection.Cards)
                card.IsLocked = false;
        }

        return collection;
    }

    /// <returns>
    ///     <see langword="true"/> if the file existed and the database had no bot state yet.
    /// </returns>
    private async Task<bool> ImportBotStateAsync(SqliteConnection connection, SqliteTransaction transaction, string path)
    {
        if (!File.Exists(path))
            return false;

        if (await SqliteCommands.ScalarAsync(connection, transaction, "SELECT COUNT(*) FROM settings;") is long and > 0)
        {
            logger.LogWarning("The database already has a bot state, skipping {File}", path);
            return false;
        }

        var legacy = ReadJson<LegacyBotState>(path);

        var packSessions = legacy.PackSessions.Select(legacySession => new PackSession(legacySession.MessageId, legacySession.UserId, legacySession.Cards)
        {
            CurrentIndex = legacySession.CurrentIndex,
            CreatedAtUtc = legacySession.CreatedAtUtc,
            // Every copy matching a saved name and rarity counts as saved, as it did before
            SavedCardIndices = legacySession.Cards
                .Select((card, index) => (card, index))
                .Where(x => legacySession.SavedCardIdentifiers.Contains($"{x.card.Name}_{x.card.Rarity}"))
                .Select(x => x.index)
                .ToHashSet(),
        }).ToList();

        await BotStateStore.WriteAsync(connection, transaction, legacy.BotActive, legacy.PullCount, legacy.LockedSets,
            new PersistedSessions { PackSessions = packSessions });
        return true;
    }

    private static T ReadJson<T>(string path) where T : new()
    {
        try
        {
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) ?? new T();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Legacy JSON file '{path}' could not be read; fix or remove it and restart.", ex);
        }
    }
}
