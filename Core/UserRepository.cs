using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Loads and saves user card collections as one JSON file per user.
/// </summary>
public sealed class UserRepository
{
    // Directory where the JSON files will be stored
    public static readonly string UserCardsDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "UserCards"
    );

    // Directory where collections of users who left a guild are kept until they come back
    private static readonly string ArchivedUserCardsDirectory = Path.Combine(UserCardsDirectory, "Left");

    /// <summary>
    ///     How long the collection of a user who left is kept before it is deleted for good.
    /// </summary>
    private static readonly TimeSpan ArchiveRetention = TimeSpan.FromDays(30);

    public UserRepository()
    {
        // Ensure the directories exist
        Directory.CreateDirectory(UserCardsDirectory);
        Directory.CreateDirectory(ArchivedUserCardsDirectory);
    }

    /// <summary>
    ///     Loads a user's card collection from a JSON file.
    /// </summary>
    /// <param name="userId">
    ///     The user ID whose card collection is to be loaded.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the user's
    ///     card collection.
    /// </returns>
    public async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
    {
        string userFilePath = Path.Combine(UserCardsDirectory, $"{userId}.json");
        string archivedFilePath = Path.Combine(ArchivedUserCardsDirectory, $"{userId}.json");

        // Restore the collection of a user who left a guild and is active again
        if (!File.Exists(userFilePath) && File.Exists(archivedFilePath))
        {
            File.Move(archivedFilePath, userFilePath);
            Console.WriteLine($"Restored archived JSON file for user {userId}.");
        }

        if (File.Exists(userFilePath))
        {
            var json = await File.ReadAllTextAsync(userFilePath);
            return JsonConvert.DeserializeObject<UserCardCollection>(json) ?? new UserCardCollection();
        }

        // Return an empty collection if file doesn't exist
        return new UserCardCollection { UserId = userId, Cards = [] };
    }

    /// <summary>
    ///     Saves a user's card collection to a JSON file.
    /// </summary>
    /// <param name="collection">
    ///     The user's card collection to be saved.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public async Task SaveUserCardsAsync(UserCardCollection collection)
    {
        string userFilePath = Path.Combine(UserCardsDirectory, $"{collection.UserId}.json");

        var json = JsonConvert.SerializeObject(collection, Formatting.Indented);
        await File.WriteAllTextAsync(userFilePath, json);
    }

    /// <summary>
    ///     Moves a user's card collection into the archive directory instead of deleting it, so it
    ///     is restored on the next load if the user is still active (e.g. in another guild) or rejoins.
    /// </summary>
    /// <param name="userId">
    ///     The user ID whose card collection is to be archived.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if a collection was archived; otherwise, <see langword="false"/>.
    /// </returns>
    public bool ArchiveUserCards(ulong userId)
    {
        string userFilePath = Path.Combine(UserCardsDirectory, $"{userId}.json");

        if (!File.Exists(userFilePath))
            return false;

        string archivedFilePath = Path.Combine(ArchivedUserCardsDirectory, $"{userId}.json");
        File.Move(userFilePath, archivedFilePath, overwrite: true);

        // A move keeps the last save time; stamp the archive time so the purge counts from here
        File.SetLastWriteTimeUtc(archivedFilePath, DateTime.UtcNow);
        return true;
    }

    /// <summary>
    ///     Deletes archived collections of users who left more than <see cref="ArchiveRetention"/>
    ///     ago and never came back, then repeats once a day. Never throws.
    /// </summary>
    /// <returns>
    ///     A task that runs for the lifetime of the process.
    /// </returns>
    public async Task RunArchivePurgeLoopAsync()
    {
        while (true)
        {
            try
            {
                foreach (var file in Directory.GetFiles(ArchivedUserCardsDirectory, "*.json"))
                {
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) <= ArchiveRetention)
                        continue;

                    File.Delete(file);
                    Console.WriteLine($"Purged archived JSON file {Path.GetFileName(file)} (left more than {ArchiveRetention.TotalDays} days ago).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to purge archived user files: {ex}");
            }

            await Task.Delay(TimeSpan.FromDays(1));
        }
    }
}
