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

    static CardStorage()
    {
        // Ensure the directory exists
        if (!Directory.Exists(UserCardsDirectory))
        {
            Directory.CreateDirectory(UserCardsDirectory);
        }
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
}