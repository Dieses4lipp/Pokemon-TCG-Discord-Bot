namespace DiscordBot.Models;

/// <summary>
///     The sessions that have to survive a restart, stored as one JSON value in the settings table.
/// </summary>
public class PersistedSessions
{
    /// <summary>
    ///     Gets or sets the pending trades, each stored once.
    /// </summary>
    public List<TradeSession> ActiveTrades { get; set; } = [];

    /// <summary>
    ///     Gets or sets the pulled (already paid) packs whose cards have not been sold yet.
    /// </summary>
    public List<PackSession> PackSessions { get; set; } = [];
}
