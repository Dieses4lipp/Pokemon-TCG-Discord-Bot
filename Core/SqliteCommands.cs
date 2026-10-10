using Microsoft.Data.Sqlite;

namespace DiscordBot.Core;

/// <summary>
///     Runs parameterized SQL on an open connection.
/// </summary>
internal static class SqliteCommands
{
    /// <returns>
    ///     The number of rows changed.
    /// </returns>
    public static async Task<int> ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    /// <returns>
    ///     The first column of the first row, or <see langword="null"/> if there is none.
    /// </returns>
    public static async Task<object?> ScalarAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Create(connection, transaction, sql, parameters);
        return await command.ExecuteScalarAsync();
    }

    /// <summary>
    ///     Creates a command with the parameters bound; <see langword="null"/> values become SQL NULL.
    ///     The caller disposes it.
    /// </summary>
    public static SqliteCommand Create(SqliteConnection connection, SqliteTransaction? transaction, string sql,
        params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}
