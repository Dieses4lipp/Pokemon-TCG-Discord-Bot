namespace DiscordBot.Core;

/// <summary>
///     Bot-wide runtime state: on/off switch, locked sets and global counters.
/// </summary>
public sealed class BotState
{
    private readonly object _lockedSetsLock = new();
    private readonly HashSet<string> _lockedSets = [];
    private int _pullCount;

    /// <summary>
    ///     Gets when this process started.
    /// </summary>
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets whether the bot is active and responding to commands.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Gets the number of packs pulled by all users.
    /// </summary>
    public int PullCount => Volatile.Read(ref _pullCount);

    /// <summary>
    ///     Counts one more pulled pack.
    /// </summary>
    public void IncrementPullCount() => Interlocked.Increment(ref _pullCount);

    /// <summary>
    ///     Gets a snapshot of the IDs of the sets that cannot be pulled.
    /// </summary>
    public IReadOnlyList<string> LockedSets
    {
        get
        {
            lock (_lockedSetsLock) return [.. _lockedSets];
        }
    }

    public bool IsSetLocked(string setId)
    {
        lock (_lockedSetsLock) return _lockedSets.Contains(setId);
    }

    /// <returns>
    ///     <see langword="true"/> if the set was not locked before.
    /// </returns>
    public bool LockSet(string setId)
    {
        lock (_lockedSetsLock) return _lockedSets.Add(setId);
    }

    /// <returns>
    ///     <see langword="true"/> if the set was locked before.
    /// </returns>
    public bool UnlockSet(string setId)
    {
        lock (_lockedSetsLock) return _lockedSets.Remove(setId);
    }

    /// <summary>
    ///     Replaces the state with values restored from disk.
    /// </summary>
    public void Restore(bool isActive, int pullCount, IEnumerable<string> lockedSets)
    {
        IsActive = isActive;
        Volatile.Write(ref _pullCount, pullCount);

        lock (_lockedSetsLock)
        {
            _lockedSets.Clear();
            _lockedSets.UnionWith(lockedSets);
        }
    }
}
