namespace DiscordBot.Core.Storage;

/// <summary>
///     The database engine the bot stores its data in (DB_PROVIDER).
/// </summary>
public enum DatabaseProvider
{
    Sqlite,
    Postgres,
}
