using DiscordBot.Commands.SlashCommandHandlers.TrainerCommands.PullCommand;
using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Persists the runtime state (bot on/off, locked sets, pull count, pending trades and paid
///     pack sessions) to a JSON file on the data volume, so a restart or redeploy does not lose it.
/// </summary>
public static class BotStateStore
{
    /// <summary>
    ///     Pack sessions older than this are not restored; their ephemeral messages are gone by then.
    /// </summary>
    private static readonly TimeSpan PackSessionMaxAge = TimeSpan.FromHours(24);

    private static readonly string StateDirectory = Path.Combine(CardStorage.UserCardsDirectory, "State");
    private static readonly string StateFilePath = Path.Combine(StateDirectory, "botState.json");

    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    /// <summary>
    ///     Restores the runtime state from disk. Does nothing if no state was saved yet.
    /// </summary>
    public static void Load()
    {
        if (!File.Exists(StateFilePath))
        {
            Console.WriteLine("No saved bot state found, starting with defaults.");
            return;
        }

        var state = JsonConvert.DeserializeObject<PersistedBotState>(File.ReadAllText(StateFilePath))
            ?? new PersistedBotState();

        CommandHandler.BotActive = state.BotActive;
        CommandHandler.PullCount = state.PullCount;

        CommandHandler.LockedSets.Clear();
        CommandHandler.LockedSets.UnionWith(state.LockedSets);

        CommandHandler.ActiveTrades.Clear();
        foreach (var trade in state.ActiveTrades)
        {
            CommandHandler.ActiveTrades[trade.SenderId] = trade;
            CommandHandler.ActiveTrades[trade.ReceiverId] = trade;
        }

        PullReactionHandler.ActiveSessions.Clear();
        foreach (var session in state.PackSessions.Where(IsRecent))
        {
            PullReactionHandler.ActiveSessions[session.MessageId] = session;
        }

        Console.WriteLine(
            $"Restored bot state: active={state.BotActive}, {state.LockedSets.Count} locked sets, " +
            $"{state.ActiveTrades.Count} trades, {PullReactionHandler.ActiveSessions.Count} pack sessions.");
    }

    /// <summary>
    ///     Writes the current runtime state to disk. Call after every change to that state.
    ///     Writes go to a temp file first, so a crash mid-write never leaves a broken state file.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public static async Task SaveAsync()
    {
        await WriteLock.WaitAsync();
        try
        {
            var state = new PersistedBotState
            {
                BotActive = CommandHandler.BotActive,
                PullCount = CommandHandler.PullCount,
                LockedSets = [.. CommandHandler.LockedSets],
                // Each trade is stored under both user IDs, keep it once
                ActiveTrades = CommandHandler.ActiveTrades.Values.ToArray().Distinct().ToList(),
                PackSessions = PullReactionHandler.ActiveSessions.Values.ToArray().Where(IsRecent).ToList(),
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
            WriteLock.Release();
        }
    }

    private static bool IsRecent(PackSession session) =>
        DateTime.UtcNow - session.CreatedAtUtc <= PackSessionMaxAge;
}
