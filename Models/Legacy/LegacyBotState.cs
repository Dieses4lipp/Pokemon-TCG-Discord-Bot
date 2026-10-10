namespace DiscordBot.Models.Legacy;

/// <summary>
///     The runtime state as stored in <c>UserCards/State/botState.json</c> before the SQLite
///     database. Its pending trades are not imported: they reference cards by name and rarity, and
///     a trade costs nothing to propose again.
/// </summary>
public class LegacyBotState
{
    public bool BotActive { get; set; } = true;

    public List<string> LockedSets { get; set; } = [];

    public int PullCount { get; set; }

    public List<LegacyPackSession> PackSessions { get; set; } = [];
}
