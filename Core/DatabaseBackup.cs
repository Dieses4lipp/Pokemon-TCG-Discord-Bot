using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Core;

/// <summary>
///     Writes a consistent copy of the SQLite database to <c>backups/</c> next to it once a day and
///     keeps the newest few. <c>ops/pokebot-backup.sh</c> ships the newest copy off the host. A
///     Postgres database is backed up by whoever runs the server.
/// </summary>
public sealed class DatabaseBackup(BotDatabase database, ILogger<DatabaseBackup> logger)
{
    private const string FilePrefix = "bot-";
    private const string FileTimestampFormat = "yyyyMMdd-HHmmss";

    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    /// <summary>
    ///     How many daily copies stay on the data volume.
    /// </summary>
    public const int KeepCount = 7;

    /// <summary>
    ///     Gets the directory the copies are written to.
    /// </summary>
    public string BackupDirectory { get; } =
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(
            database.SqlitePath ?? throw new InvalidOperationException("Only an SQLite database is backed up by the bot.")))!, "backups");

    /// <summary>
    ///     Writes a copy and deletes all but the newest <see cref="KeepCount"/>.
    /// </summary>
    /// <param name="nowUtc">
    ///     The current time, used for the file name.
    /// </param>
    /// <returns>
    ///     The path of the new copy.
    /// </returns>
    public async Task<string> CreateAsync(DateTime nowUtc)
    {
        Directory.CreateDirectory(BackupDirectory);

        string path = Path.Combine(BackupDirectory,
            $"{FilePrefix}{nowUtc.ToString(FileTimestampFormat, CultureInfo.InvariantCulture)}.db");
        // VACUUM INTO fails if the target exists, e.g. a second backup within the same second
        File.Delete(path);

        // Unlike copying the file, VACUUM INTO includes what is still in the WAL and is consistent
        // while the bot keeps writing
        await using (var db = database.CreateContext())
        {
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {path};");
        }

        // The timestamp in the name sorts in time order
        foreach (var old in Directory.GetFiles(BackupDirectory, $"{FilePrefix}*.db").OrderDescending().Skip(KeepCount))
            File.Delete(old);

        return path;
    }

    /// <summary>
    ///     Gets how long until the next copy is due: a day after the newest one, so frequent
    ///     restarts (every deploy is one) do not push the older days out of the kept copies.
    /// </summary>
    public TimeSpan TimeUntilNextBackup(DateTime nowUtc)
    {
        var newest = Directory.Exists(BackupDirectory)
            ? Directory.GetFiles(BackupDirectory, $"{FilePrefix}*.db").Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max()
            : default;

        var due = newest + Interval - nowUtc;
        return due > TimeSpan.Zero ? due : TimeSpan.Zero;
    }

    /// <summary>
    ///     Runs <see cref="CreateAsync"/> once a day. Never throws.
    /// </summary>
    /// <returns>
    ///     A task that runs for the lifetime of the process.
    /// </returns>
    public async Task RunAsync()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeUntilNextBackup(DateTime.UtcNow));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to read the existing database backups");
                await Task.Delay(Interval);
            }

            try
            {
                string path = await CreateAsync(DateTime.UtcNow);
                logger.LogInformation("Wrote database backup {BackupFile} ({SizeKiB} KiB)", path, new FileInfo(path).Length / 1024);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write database backup");
                // Without a new copy the next one would be due at once; retry in an hour instead
                await Task.Delay(TimeSpan.FromHours(1));
            }
        }
    }
}
