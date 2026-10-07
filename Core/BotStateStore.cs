using DiscordBot.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Persists the runtime state (bot on/off, locked sets, pull count, pending trades and paid
///     pack sessions) to a JSON file on the data volume, so a restart or redeploy does not lose it.
/// </summary>
public sealed class BotStateStore(BotState botState, SessionStore sessions, ILogger<BotStateStore> logger)
{
    private static readonly string StateDirectory = Path.Combine(UserRepository.UserCardsDirectory, "State");
    private static readonly string StateFilePath = Path.Combine(StateDirectory, "botState.json");

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>
    ///     Restores the runtime state from disk. Does nothing if no state was saved yet.
    /// </summary>
    public void Load()
    {
        if (!File.Exists(StateFilePath))
        {
            logger.LogInformation("No saved bot state found, starting with defaults");
            return;
        }

        var state = JsonConvert.DeserializeObject<PersistedBotState>(File.ReadAllText(StateFilePath))
            ?? new PersistedBotState();

        botState.Restore(state.BotActive, state.PullCount, state.LockedSets);
        sessions.Restore(state.PackSessions, state.ActiveTrades);
        sessions.RemoveExpired(DateTime.UtcNow);

        logger.LogInformation(
            "Restored bot state: active={BotActive}, {LockedSetCount} locked sets, {TradeCount} trades, {PackSessionCount} pack sessions",
            state.BotActive, state.LockedSets.Count, sessions.Trades.Count, sessions.Packs.Count);
    }

    /// <summary>
    ///     Writes the current runtime state to disk. Call after every change to that state.
    ///     Writes go to a temp file first, so a crash mid-write never leaves a broken state file.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task SaveAsync()
    {
        await _writeLock.WaitAsync();
        try
        {
            var state = new PersistedBotState
            {
                BotActive = botState.IsActive,
                PullCount = botState.PullCount,
                LockedSets = [.. botState.LockedSets],
                ActiveTrades = [.. sessions.Trades],
                PackSessions = [.. sessions.Packs.Values],
            };

            Directory.CreateDirectory(StateDirectory);
            string tempPath = StateFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, JsonConvert.SerializeObject(state, Formatting.Indented));
            File.Move(tempPath, StateFilePath, overwrite: true);
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
}
