using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Persists the runtime state (bot on/off, locked sets, pull count, pending trades and paid
///     pack sessions) to a JSON file on the data volume, so a restart or redeploy does not lose it.
/// </summary>
public sealed class BotStateStore(BotState botState, SessionStore sessions)
{
    /// <summary>
    ///     Pack sessions older than this are not restored; their ephemeral messages are gone by then.
    /// </summary>
    private static readonly TimeSpan PackSessionMaxAge = TimeSpan.FromHours(24);

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
            Console.WriteLine("No saved bot state found, starting with defaults.");
            return;
        }

        var state = JsonConvert.DeserializeObject<PersistedBotState>(File.ReadAllText(StateFilePath))
            ?? new PersistedBotState();

        botState.Restore(state.BotActive, state.PullCount, state.LockedSets);
        sessions.Restore(state.PackSessions.Where(IsRecent), state.ActiveTrades);

        Console.WriteLine(
            $"Restored bot state: active={state.BotActive}, {state.LockedSets.Count} locked sets, " +
            $"{state.ActiveTrades.Count} trades, {sessions.Packs.Count} pack sessions.");
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
                PackSessions = sessions.Packs.Values.Where(IsRecent).ToList(),
            };

            Directory.CreateDirectory(StateDirectory);
            string tempPath = StateFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, JsonConvert.SerializeObject(state, Formatting.Indented));
            File.Move(tempPath, StateFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save bot state: {ex}");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static bool IsRecent(PackSession session) =>
        DateTime.UtcNow - session.CreatedAtUtc <= PackSessionMaxAge;
}
