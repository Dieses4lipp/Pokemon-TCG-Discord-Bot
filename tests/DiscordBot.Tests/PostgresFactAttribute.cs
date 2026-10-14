namespace DiscordBot.Tests;

/// <summary>
///     A test against a real Postgres server, see <see cref="PostgresServer"/>. Skipped on a machine
///     with neither POKEBOT_TEST_POSTGRES nor a reachable Docker; never skipped in CI.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (!PostgresServer.IsAvailable && Environment.GetEnvironmentVariable("CI") != "true")
            Skip = "No Postgres: set POKEBOT_TEST_POSTGRES to a server's connection string, or run Docker.";
    }
}
