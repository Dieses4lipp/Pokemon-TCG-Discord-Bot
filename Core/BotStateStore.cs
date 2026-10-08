using System.Globalization;
using DiscordBot.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Persists the runtime state (bot on/off, locked sets, pull count, pending trades and paid
///     pack sessions) in the settings and locked_sets tables, so a restart or redeploy does not lose it.
/// </summary>
public sealed class BotStateStore(BotDatabase database, BotState botState, SessionStore sessions, ILogger<BotStateStore> logger)
{
    private const string BotActiveKey = "bot_active";
    private const string PullCountKey = "pull_count";
    private const string SessionsKey = "sessions";

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    ///     Restores the runtime state from the database. Keeps the defaults if nothing was saved yet.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task LoadAsync()
    {
        await using var connection = await database.OpenAsync();

        var settings = new Dictionary<string, string>();
        await using (var command = SqliteCommands.Create(connection, null, "SELECT key, value FROM settings;"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                settings[reader.GetString(0)] = reader.GetString(1);
        }

        var lockedSets = new List<string>();
        await using (var command = SqliteCommands.Create(connection, null, "SELECT set_id FROM locked_sets;"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                lockedSets.Add(reader.GetString(0));
        }

        if (!settings.ContainsKey(BotActiveKey))
        {
            logger.LogInformation("No saved bot state found, starting with defaults");
            return;
        }

        var persistedSessions = settings.TryGetValue(SessionsKey, out var sessionsJson)
            ? JsonConvert.DeserializeObject<PersistedSessions>(sessionsJson) ?? new PersistedSessions()
            : new PersistedSessions();

        botState.Restore(
            settings[BotActiveKey] == "1",
            settings.TryGetValue(PullCountKey, out var pullCount) ? int.Parse(pullCount, CultureInfo.InvariantCulture) : 0,
            lockedSets);
        sessions.Restore(persistedSessions.PackSessions, persistedSessions.ActiveTrades);
        sessions.RemoveExpired(DateTime.UtcNow);

        logger.LogInformation(
            "Restored bot state: active={BotActive}, {LockedSetCount} locked sets, {TradeCount} trades, {PackSessionCount} pack sessions",
            botState.IsActive, lockedSets.Count, sessions.Trades.Count, sessions.Packs.Count);
    }

    /// <summary>
    ///     Writes the current runtime state to the database. Call after every change to that state.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task SaveAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            await using var connection = await database.OpenAsync();
            await using var transaction = connection.BeginTransaction();
            await WriteAsync(connection, transaction, botState.IsActive, botState.PullCount, botState.LockedSets,
                new PersistedSessions
                {
                    ActiveTrades = [.. sessions.Trades],
                    PackSessions = [.. sessions.Packs.Values],
                });
            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save bot state");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    ///     Replaces the stored runtime state inside a transaction the caller owns.
    /// </summary>
    internal static async Task WriteAsync(SqliteConnection connection, SqliteTransaction transaction,
        bool botActive, int pullCount, IEnumerable<string> lockedSets, PersistedSessions persistedSessions)
    {
        (string Key, string Value)[] settings =
        [
            (BotActiveKey, botActive ? "1" : "0"),
            (PullCountKey, pullCount.ToString(CultureInfo.InvariantCulture)),
            (SessionsKey, JsonConvert.SerializeObject(persistedSessions)),
        ];

        foreach (var (key, value) in settings)
        {
            await SqliteCommands.ExecuteAsync(connection, transaction,
                "INSERT INTO settings (key, value) VALUES ($key, $value) ON CONFLICT (key) DO UPDATE SET value = excluded.value;",
                ("$key", key), ("$value", value));
        }

        await SqliteCommands.ExecuteAsync(connection, transaction, "DELETE FROM locked_sets;");
        foreach (var setId in lockedSets)
        {
            await SqliteCommands.ExecuteAsync(connection, transaction,
                "INSERT OR IGNORE INTO locked_sets (set_id) VALUES ($setId);", ("$setId", setId));
        }
    }
}
