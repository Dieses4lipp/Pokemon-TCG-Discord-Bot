namespace DiscordBot.Models;

/// <summary>
///     Represents the runtime state of the bot that has to survive a restart, as written to the
///     bot state JSON file.
/// </summary>
public class PersistedBotState
{
    /// <summary>
    ///     Gets or sets whether the bot is active and responding to commands.
    /// </summary>
    public bool BotActive { get; set; } = true;

    /// <summary>
    ///     Gets or sets the IDs of the sets that cannot be pulled.
    /// </summary>
    public List<string> LockedSets { get; set; } = [];

    /// <summary>
    ///     Gets or sets the number of packs pulled by all users.
    /// </summary>
    public int PullCount { get; set; }

    /// <summary>
    ///     Gets or sets the pending trades, each stored once.
    /// </summary>
    public List<TradeSession> ActiveTrades { get; set; } = [];

    /// <summary>
    ///     Gets or sets the pulled (already paid) packs whose cards have not been sold yet.
    /// </summary>
    public List<PackSession> PackSessions { get; set; } = [];
}
