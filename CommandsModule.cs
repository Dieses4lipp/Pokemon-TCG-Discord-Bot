using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Linq;

namespace DiscordBot
{
    public class CommandsModule : ModuleBase<SocketCommandContext>
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string ApiUrl = "https://api.pokemontcg.io/v2/cards";
        private static readonly string SetsApiUrl = "https://api.pokemontcg.io/v2/sets";
        public static readonly Dictionary<ulong, PackSession> ActiveSessions = new();
        private static readonly Dictionary<ulong, SetSession> ActiveSetSessions = new();


        private static readonly Dictionary<string, double> RarityChances = new()
        {
            {"Common", 0.50 },
            {"Uncommon", 0.20 },
            {"Rare", 0.15 },
            {"Rare Holo", 0.05 },
            {"Ultra Rare", 0.07 },
            {"Secret Rare", 0.03 }
        };

        [Command("mycards")]
        public async Task MyCardsAsync()
        {
            // Load the user's saved card collection from JSON
            var collection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
            if (collection.Cards == null || collection.Cards.Count == 0)
            {
                await ReplyAsync("You don't have any saved cards!");
                return;
            }

            // Build an embed using the first saved card
            var embed = BuildCardEmbed(collection.Cards[0], 1, collection.Cards.Count);
            var message = await ReplyAsync(embed: embed);

            // Create a session for navigating through the user's cards
            var session = new PackSession(message.Id, Context.User.Id, collection.Cards);
            ActiveSessions[message.Id] = session;

            // Add reactions for navigation
            await message.AddReactionAsync(new Emoji("◀️"));
            await message.AddReactionAsync(new Emoji("▶️"));
            await message.AddReactionAsync(new Emoji("🗑️"));
        }

        [Command("pullcard")]
        public async Task PullCardAsync(string setId = null)
        {
            try
            {
                var cardData = await GetRandomCards(1, setId);
                if (cardData.Count == 0)
                {
                    await ReplyAsync(setId == null
                        ? "No cards found!"
                        : $"No cards found for set: {setId}!");
                    return;
                }

                var card = cardData[0];
                var embed = BuildCardEmbed(card, 1, 1);
                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync($"An error occurred while retrieving the card: {ex.Message}");
            }
        }

        [Command("pullpack")]
        public async Task PullPackAsync(string setId)
        {
            try
            {
                var allCards = await GetRandomCards(100, setId);
                if (allCards.Count == 0)
                {
                    await ReplyAsync($"No cards found for set: {setId}!");
                    return;
                }

                var random = new Random();
                var selectedCards = new HashSet<Card>();
                while (selectedCards.Count < 9)
                {
                    string selectedRarity = RollRarity(random);
                    var possibleCards = allCards.Where(c => c.Rarity == selectedRarity).ToList();

                    var cardToAdd = possibleCards.Count > 0
                        ? possibleCards[random.Next(possibleCards.Count)]
                        : allCards[random.Next(allCards.Count)];

                    selectedCards.Add(cardToAdd);
                }

                var selectedCardList = selectedCards.ToList();
                var embed = BuildCardEmbed(selectedCardList[0], 1, selectedCardList.Count);
                var message = await ReplyAsync(embed: embed);

                var session = new PackSession(message.Id, Context.User.Id, selectedCardList);
                ActiveSessions[message.Id] = session;

                await message.AddReactionAsync(new Emoji("◀️"));
                await message.AddReactionAsync(new Emoji("▶️"));
                await message.AddReactionAsync(new Emoji("💾")); // Add the 💾 reaction for saving the card
            }
            catch (Exception ex)
            {
                await ReplyAsync($"An error occurred while retrieving the pack: {ex.Message}");
            }
        }


        private string RollRarity(Random random)
        {
            double roll = random.NextDouble();
            double cumulative = 0.0;

            foreach (var rarity in RarityChances)
            {
                cumulative += rarity.Value;
                if (roll <= cumulative)
                {
                    return rarity.Key;
                }
            }

            return "Common";
        }

        public static Embed BuildCardEmbed(Card card, int current, int total)
        {
            return new EmbedBuilder()
                .WithTitle($"{card.Name} ({current}/{total})")
                .WithDescription($"Rarity: {card.Rarity}")
                .WithImageUrl(card.Images.Small)
                .WithColor(Color.Blue)
                .Build();
        }


        public async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot) return;
            if (!ActiveSessions.ContainsKey(reaction.MessageId)) return;

            var session = ActiveSessions[reaction.MessageId];
            if (reaction.UserId != session.UserId) return;

            var message = await cache.GetOrDownloadAsync();

            if (reaction.Emote.Name == "▶️" || reaction.Emote.Name == "◀️")
            {
                // Handle switching between cards
                session.CurrentIndex = (reaction.Emote.Name == "▶️")
                    ? (session.CurrentIndex + 1) % session.Cards.Count
                    : (session.CurrentIndex - 1 + session.Cards.Count) % session.Cards.Count;

                await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));
            }
            else if (reaction.Emote.Name == "💾")
            {
                // Handle saving the card
                var cardToSave = session.Cards[session.CurrentIndex];

                // Load user's card collection
                var collection = await CardStorage.LoadUserCardsAsync(session.UserId);

                // Check if the user already has 10 cards
                if (collection.Cards.Count >= 10)
                {
                    var msgChannel = await channel.GetOrDownloadAsync();
                    await msgChannel.SendMessageAsync("You can only store up to 10 cards!");
                    return;
                }

                // Add the current card to the user's collection
                collection.Cards.Add(cardToSave);

                // Save the updated collection to the user's JSON file
                await CardStorage.SaveUserCardsAsync(collection);

                var channelForMsg = await channel.GetOrDownloadAsync();
                await channelForMsg.SendMessageAsync($"Card '{cardToSave.Name}' has been saved to your collection!");
            }
            else if (reaction.Emote.Name == "🗑️")
            {
                // Handle deleting the card from the saved collection
                var cardToDelete = session.Cards[session.CurrentIndex];

                // Load user's card collection from JSON
                var collection = await CardStorage.LoadUserCardsAsync(session.UserId);

                // Remove the card. Here we match by name and rarity (adjust if needed for uniqueness).
                int removedCount = collection.Cards.RemoveAll(c => c.Name == cardToDelete.Name && c.Rarity == cardToDelete.Rarity);
                if (removedCount > 0)
                {
                    // Save the updated collection
                    await CardStorage.SaveUserCardsAsync(collection);

                    // Also remove the card from the session's list
                    session.Cards.RemoveAt(session.CurrentIndex);

                    // If no cards remain, delete the message and remove the session
                    if (session.Cards.Count == 0)
                    {
                        await message.DeleteAsync();
                        ActiveSessions.Remove(message.Id);
                        return;
                    }

                    // Adjust the index if necessary
                    if (session.CurrentIndex >= session.Cards.Count)
                        session.CurrentIndex = session.Cards.Count - 1;

                    // Update the embed with the new current card
                    await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));

                    var channelForMsg = await channel.GetOrDownloadAsync();
                    await channelForMsg.SendMessageAsync($"Card '{cardToDelete.Name}' has been removed from your collection!");
                }
                else
                {
                    var channelForMsg = await channel.GetOrDownloadAsync();
                    await channelForMsg.SendMessageAsync("Card not found in your collection.");
                }
            }

            // Remove the reaction after processing it
            await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
        }


        public static async Task SaveUserCardsAsync(UserCardCollection collection)
        {
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{collection.UserId}.json");

            var json = JsonConvert.SerializeObject(collection, Formatting.Indented);
            await File.WriteAllTextAsync(userFilePath, json);
        }

        public static async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
        {
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{userId}.json");

            if (File.Exists(userFilePath))
            {
                var json = await File.ReadAllTextAsync(userFilePath);
                return JsonConvert.DeserializeObject<UserCardCollection>(json) ?? new UserCardCollection();
            }

            return new UserCardCollection { UserId = userId, Cards = new List<Card>() }; // Return an empty collection if file doesn't exist
        }

        public async Task<List<Card>> GetRandomCards(int count, string setId)
        {
            var random = new Random();
            const int pageSize = 250;
            int randomPage = random.Next(1, 10);

            string requestUrl = setId == null
                ? $"{ApiUrl}?page={randomPage}&pageSize={pageSize}"
                : $"{ApiUrl}?q=set.id%3A{setId}&pageSize={pageSize}";

            try
            {
                var response = await _httpClient.GetStringAsync(requestUrl);
                var cardData = JsonConvert.DeserializeObject<ApiResponse>(response);

                return cardData?.Data?.OrderBy(_ => random.Next()).Take(count).ToList() ?? new List<Card>();
            }
            catch (Exception ex)
            {
                return new List<Card>();
            }
        }

        [Command("sets")]
        public async Task GetAllSetsAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(SetsApiUrl);
                var setData = JsonConvert.DeserializeObject<SetApiResponse>(response);

                if (setData?.Data == null || setData.Data.Count == 0)
                {
                    await ReplyAsync("No sets found!");
                    return;
                }

                var selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Select a set")
                    .WithCustomId("set_selection")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (var set in setData.Data.Take(25))
                {
                    selectMenu.AddOption(set.Name, set.Id.ToString(), $"ID: {set.Id}");
                }

                var component = new ComponentBuilder()
                    .WithSelectMenu(selectMenu);

                var embed = new EmbedBuilder()
                    .WithTitle("Pokémon Karten Sets")
                    .WithDescription("Die Sets werden angezeigt - Dies ist keine Auswahl nur zur Informationen für die Pack Set IDs")
                    .WithColor(Color.Green)
                    .Build();

                await ReplyAsync(embed: embed, components: component.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred: " + ex.Message);
            }
        }

        public async Task HandleSetReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot) return;

            if (ActiveSetSessions.ContainsKey(reaction.MessageId))
            {
                var session = ActiveSetSessions[reaction.MessageId];
                if (reaction.UserId != session.UserId) return;

                session.CurrentIndex = (reaction.Emote.Name == "▶️")
                    ? (session.CurrentIndex + 1) % session.Sets.Count
                    : (session.CurrentIndex - 1 + session.Sets.Count) % session.Sets.Count;

                var message = await cache.GetOrDownloadAsync();
                await message.ModifyAsync(m => m.Embed = BuildSetEmbed(session.Sets[session.CurrentIndex], session.CurrentIndex + 1, session.Sets.Count));
                await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
            }
        }
        public static Task HandleUserLeft(SocketGuildUser user)
        {
            // Construct the path to the user's JSON file
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory, $"{user.Id}.json");

            if (File.Exists(userFilePath))
            {
                File.Delete(userFilePath);
                Console.WriteLine($"Deleted JSON file for user {user.Username} ({user.Id}).");
            }
            else
            {
                Console.WriteLine($"No JSON file found for user {user.Username} ({user.Id}).");
            }

            return Task.CompletedTask;
        }

        private static Embed BuildSetEmbed(Set set, int current, int total)
        {
            return new EmbedBuilder()
                .WithTitle($"Set: {set.Name} ({current}/{total})")
                .WithDescription($"ID: `{set.Id}`")
                .WithThumbnailUrl(string.IsNullOrEmpty(set.ImageUrl) ? "https://images.pokemontcg.io/base1/symbol.png" : set.ImageUrl)
                .WithColor(Color.Green)
                .Build();
        }

        private class SetSession
        {
            public ulong MessageId { get; }
            public ulong UserId { get; }
            public List<Set> Sets { get; }
            public int CurrentIndex { get; set; }

            public SetSession(ulong messageId, ulong userId, List<Set> sets)
            {
                MessageId = messageId;
                UserId = userId;
                Sets = sets;
                CurrentIndex = 0;
            }
        }


        public class PackSession
        {
            public ulong MessageId { get; }
            public ulong UserId { get; }
            public List<Card> Cards { get; }
            public int CurrentIndex { get; set; }

            public PackSession(ulong messageId, ulong userId, List<Card> cards)
            {
                MessageId = messageId;
                UserId = userId;
                Cards = cards;
                CurrentIndex = 0;
            }
        }

        public class ApiResponse
        {
            public List<Card> Data { get; set; }
        }

        public class SetApiResponse
        {
            public List<Set> Data { get; set; }
        }

        public class Set
        {
            public string Name { get; set; }
            public string Id { get; set; }
            public string ImageUrl { get; set; } 

        }


        public class Images
        {
            public string Small { get; set; }
        }
    }
}
