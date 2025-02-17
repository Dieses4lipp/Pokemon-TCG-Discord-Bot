using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DiscordBot
    {

        public static class CardStorage
        {
        // Directory where the JSON files will be stored
        public static string UserCardsDirectory = Path.Combine(
        Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName,
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

            // Load the user's card collection from a JSON file
            public static async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
            {
                string userFilePath = Path.Combine(UserCardsDirectory, $"{userId}.json");

                if (File.Exists(userFilePath))
                {
                    var json = await File.ReadAllTextAsync(userFilePath);
                    return JsonConvert.DeserializeObject<UserCardCollection>(json) ?? new UserCardCollection();
                }

                return new UserCardCollection { UserId = userId, Cards = new List<Card>() }; // Return an empty collection if file doesn't exist
            }

            // Save the user's card collection to a JSON file
            public static async Task SaveUserCardsAsync(UserCardCollection collection)
            {
                string userFilePath = Path.Combine(UserCardsDirectory, $"{collection.UserId}.json");

                var json = JsonConvert.SerializeObject(collection, Formatting.Indented);
                await File.WriteAllTextAsync(userFilePath, json);
            }
        }


   }
