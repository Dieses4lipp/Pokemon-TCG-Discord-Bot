using DiscordBot.Models;
using Newtonsoft.Json;

namespace DiscordBot.Core;

/// <summary>
///     Provides methods to load and save user card collections to JSON files.
/// </summary>
public static class CardStorage
{
    // Directory where the JSON files will be stored
    public static string UserCardsDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "UserCards"
    );

    // Directory where collections of users who left a guild are kept until they come back
    public static string ArchivedUserCardsDirectory = Path.Combine(UserCardsDirectory, "Left");

    static CardStorage()
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
    public static async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
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
    public static async Task SaveUserCardsAsync(UserCardCollection collection)
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
    public static bool ArchiveUserCards(ulong userId)
    {
        string userFilePath = Path.Combine(UserCardsDirectory, $"{userId}.json");

        if (!File.Exists(userFilePath))
            return false;

        File.Move(userFilePath, Path.Combine(ArchivedUserCardsDirectory, $"{userId}.json"), overwrite: true);
        return true;
    }
}