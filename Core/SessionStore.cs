using System.Collections.Concurrent;
using DiscordBot.Models;

namespace DiscordBot.Core;

/// <summary>
///     Holds the open pack views, inventory views and pending trades.
/// </summary>
public sealed class SessionStore
{
    /// <summary>
    ///     Sessions older than this are dropped; the ephemeral pack and inventory messages are
    ///     long gone from the user's client by then.
    /// </summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    // Each trade is stored under both user IDs, so either side can look it up
    private readonly ConcurrentDictionary<ulong, TradeSession> _tradesByUser = new();

    /// <summary>
    ///     Gets the pulled (already paid) packs, by message ID.
    /// </summary>
    public ConcurrentDictionary<ulong, PackSession> Packs { get; } = new();

    /// <summary>
    ///     Gets the open /inventory views, by message ID.
    /// </summary>
    public ConcurrentDictionary<ulong, PackSession> Inventories { get; } = new();

    /// <summary>
    ///     Gets the pending trades, each once.
    /// </summary>
    public IReadOnlyList<TradeSession> Trades => _tradesByUser.Values.Distinct().ToList();

    public bool TryGetTrade(ulong userId, out TradeSession session) =>
        _tradesByUser.TryGetValue(userId, out session!);

    public bool IsTrading(ulong userId) => _tradesByUser.ContainsKey(userId);

    public void AddTrade(TradeSession session)
    {
        _tradesByUser[session.SenderId] = session;
        _tradesByUser[session.ReceiverId] = session;
    }

    public void RemoveTrade(TradeSession session)
    {
        _tradesByUser.TryRemove(new KeyValuePair<ulong, TradeSession>(session.SenderId, session));
        _tradesByUser.TryRemove(new KeyValuePair<ulong, TradeSession>(session.ReceiverId, session));
    }

    /// <summary>
    ///     Removes every session older than <see cref="SessionLifetime"/>.
    /// </summary>
    /// <param name="nowUtc">
    ///     The current time.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if a persisted session (pack or trade) was removed.
    /// </returns>
    public bool RemoveExpired(DateTime nowUtc)
    {
        bool IsExpired(DateTime createdAtUtc) => nowUtc - createdAtUtc > SessionLifetime;

        foreach (var (messageId, inventory) in Inventories)
        {
            if (IsExpired(inventory.CreatedAtUtc))
                Inventories.TryRemove(messageId, out _);
        }

        bool removedPersisted = false;

        foreach (var (messageId, pack) in Packs)
        {
            if (IsExpired(pack.CreatedAtUtc))
                removedPersisted |= Packs.TryRemove(messageId, out _);
        }

        foreach (var trade in Trades)
        {
            if (!IsExpired(trade.CreatedAtUtc)) continue;
            RemoveTrade(trade);
            removedPersisted = true;
        }

        return removedPersisted;
    }

    /// <summary>
    ///     Replaces the persisted sessions with ones restored from disk.
    /// </summary>
    public void Restore(IEnumerable<PackSession> packs, IEnumerable<TradeSession> trades)
    {
        Packs.Clear();
        foreach (var pack in packs)
            Packs[pack.MessageId] = pack;

        _tradesByUser.Clear();
        foreach (var trade in trades)
            AddTrade(trade);
    }
}
