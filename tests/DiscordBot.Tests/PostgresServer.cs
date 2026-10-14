using DiscordBot.Core;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DiscordBot.Tests;

/// <summary>
///     The Postgres server the Postgres tests share: the one in POKEBOT_TEST_POSTGRES if set,
///     otherwise a throwaway container. Every test gets its own database on it.
/// </summary>
public sealed class PostgresServer : IAsyncLifetime
{
    private const string ConnectionVariable = "POKEBOT_TEST_POSTGRES";

    private PostgreSqlContainer? _container;
    private string _connectionString = "";

    public static bool IsAvailable =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } ||
        Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 } ||
        File.Exists("/var/run/docker.sock") ||
        (OperatingSystem.IsWindows() && Directory.GetFiles(@"\\.\pipe\").Any(pipe => pipe.EndsWith("docker_engine", StringComparison.OrdinalIgnoreCase)));

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } connectionString)
        {
            _connectionString = connectionString;
            return;
        }

        if (!IsAvailable)
            return;

        _container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync()
    {
        if (_container != null)
            await _container.DisposeAsync();
    }

    /// <summary>
    ///     A database of its own on the server; the first migration creates it.
    /// </summary>
    public BotDatabase CreateDatabase() =>
        BotDatabase.Postgres(new NpgsqlConnectionStringBuilder(_connectionString) { Database = $"pokebot_test_{Guid.NewGuid():N}" }.ToString());
}
