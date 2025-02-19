using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace DiscordBot
{
    /// <summary>
    ///     Handles Discord commands for interacting with Pokémon cards and sets.
    ///     This includes pulling cards and packs, navigating card collections, and
    ///     managing set selections.
    /// </summary>
    public class CommandsModule : ModuleBase<SocketCommandContext>
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string ApiUrl = "https://api.pokemontcg.io/v2/cards";
        private static readonly string SetsApiUrl = "https://api.pokemontcg.io/v2/sets";

        /// <summary>
        ///     Stores active card navigation sessions mapped by message ID.
        /// </summary>
        public static readonly Dictionary<ulong, PackSession> ActiveSessions = new();

        /// <summary>
        ///     Stores active set navigation sessions mapped by message ID.
        /// </summary>
        private static readonly Dictionary<ulong, SetSession> ActiveSetSessions = new();

        /// <summary>
        ///     Maps card rarities to their corresponding probabilities.
        /// </summary>
        private static readonly Dictionary<string, double> RarityChances = new()
        {
            {"Common", 0.50 },
            {"Uncommon", 0.20 },
            {"Rare", 0.15 },
            {"Rare Holo", 0.05 },
            {"Ultra Rare", 0.07 },
            {"Secret Rare", 0.03 }
        };

        /// <summary>
        ///     Retrieves and displays the current user's saved Pokémon cards.
        ///     The cards are shown in an embed with reactions for navigation.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Command("mycards")]
        public async Task MyCardsAsync()
        {
            // Load the user's saved card collection from JSON.
            UserCardCollection collection = await CardStorage.LoadUserCardsAsync(Context.User.Id);
            if (collection.Cards == null ||
                collection.Cards.Count == 0)
            {
                await ReplyAsync("You don't have any saved cards! Try !pullpack and save a card!");
                return;
            }

            // Build an embed using the first saved card.
            Embed embed = BuildCardEmbed(collection.Cards[0],
                1,
                collection.Cards.Count);
            IUserMessage? message = await ReplyAsync(embed: embed);

            // Create a session for navigating through the user's cards.
            var session = new PackSession(message.Id,
                Context.User.Id,
                collection.Cards);
            ActiveSessions[message.Id] = session;

            // Add reactions for navigation.
            await message.AddReactionAsync(new Emoji("◀️"));
            await message.AddReactionAsync(new Emoji("▶️"));
            await message.AddReactionAsync(new Emoji("🗑️"));
        }

        /// <summary>
        ///     Pulls a random Pokémon card from the API and displays it in an embed.
        ///     Optionally, the card can be filtered by a specific set.
        /// </summary>
        /// <param name="setId">The optional ID of the set to filter the card pull.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

        /// <summary>
        ///     Pulls a pack of 9 random Pokémon cards from the API (optionally filtered by
        ///     set)
        ///     and displays them in an embed with navigation reactions.
        /// </summary>
        /// <param name="setId">The ID of the set to filter cards by.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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
                await message.AddReactionAsync(new Emoji("💾")); // Reaction for saving the card.
            }
            catch (Exception ex)
            {
                await ReplyAsync($"An error occurred while retrieving the pack: {ex.Message}");
            }
        }

        /// <summary>
        ///     Determines the rarity of a card based on predefined probabilities.
        /// </summary>
        /// <param name="random">An instance of the random number generator.</param>
        /// <returns>A string representing the selected rarity.</returns>
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

        /// <summary>
        ///     Builds an embed to display a Pokémon card.
        /// </summary>
        /// <param name="card">The card to display.</param>
        /// <param name="current">The current index of the card within a session.</param>
        /// <param name="total">The total number of cards in the session.</param>
        /// <returns>An <see cref="Embed" /> representing the card's details.</returns>
        public static Embed BuildCardEmbed(Card card, int current, int total)
        {
            return new EmbedBuilder()
                .WithTitle($"{card.Name} ({current}/{total})")
                .WithDescription($"Rarity: {card.Rarity}")
                .WithImageUrl(card.Images.Small)
                .WithColor(Color.Blue)
                .Build();
        }

        /// <summary>
        ///     Handles reactions added to a card embed message to navigate or modify the
        ///     user's card collection.
        /// </summary>
        /// <param name="cache">The cached user message.</param>
        /// <param name="channel">The channel where the message was sent.</param>
        /// <param name="reaction">The reaction that was added.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.User.Value.IsBot)
            {
                return;
            }

            if (!ActiveSessions.ContainsKey(reaction.MessageId))
            {
                return;
            }

            var session = ActiveSessions[reaction.MessageId];
            if (reaction.UserId != session.UserId) return;

            IUserMessage? message = await cache.GetOrDownloadAsync();

            if (reaction.Emote.Name == "▶️" ||
                reaction.Emote.Name == "◀️")
            {
                // Navigate through cards.
                session.CurrentIndex = reaction.Emote.Name == "▶️"
                    ? (session.CurrentIndex + 1) % session.Cards.Count
                    : (session.CurrentIndex - 1 + session.Cards.Count) % session.Cards.Count;

                await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex],
                    session.CurrentIndex + 1,
                    session.Cards.Count));
            }
            else if (reaction.Emote.Name == "💾")
            {
                // Save the current card.
                Card cardToSave = session.Cards[session.CurrentIndex];
                UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

                if (collection.Cards.Count >= 10)
                {
                    IMessageChannel? msgChannel = await channel.GetOrDownloadAsync();
                    await msgChannel.SendMessageAsync(
                        "You can only store up to 10 cards! Try !mycards and delete a card to make room!");
                    return;
                }

                collection.Cards.Add(cardToSave);
                await CardStorage.SaveUserCardsAsync(collection);

                IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
                await channelForMsg.SendMessageAsync($"Card '{cardToSave.Name}' has been saved to your collection!");
            }
            else if (reaction.Emote.Name == "🗑️")
            {
                // Delete the current card from the collection.
                Card cardToDelete = session.Cards[session.CurrentIndex];
                UserCardCollection collection = await CardStorage.LoadUserCardsAsync(session.UserId);

                int removedCount =
                    collection.Cards.RemoveAll(c => c.Name == cardToDelete.Name && c.Rarity == cardToDelete.Rarity);
                if (removedCount > 0)
                {
                    await CardStorage.SaveUserCardsAsync(collection);
                    session.Cards.RemoveAt(session.CurrentIndex);

                    if (session.Cards.Count == 0)
                    {
                        await message.DeleteAsync();
                        ActiveSessions.Remove(message.Id);
                        return;
                    }

                    if (session.CurrentIndex >= session.Cards.Count)
                    {
                        session.CurrentIndex = session.Cards.Count - 1;
                    }

                    await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex],
                        session.CurrentIndex + 1,
                        session.Cards.Count));

                    IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
                    await channelForMsg.SendMessageAsync(
                        $"Card '{cardToDelete.Name}' has been removed from your collection!");
                }
                else
                {
                    IMessageChannel? channelForMsg = await channel.GetOrDownloadAsync();
                    await channelForMsg.SendMessageAsync(
                        "Card not found in your collection. Did you save a card with the save symbol next to the Card?");
                }
            }

            // Remove the reaction after processing.
            await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
        }

        /// <summary>
        ///     Saves the specified user's card collection to a JSON file.
        /// </summary>
        /// <param name="collection">The user card collection to save.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task SaveUserCardsAsync(UserCardCollection collection)
        {
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory,
                $"{collection.UserId}.json");
            string json = JsonConvert.SerializeObject(collection,
                Formatting.Indented);
            await File.WriteAllTextAsync(userFilePath,
                json);
        }

        /// <summary>
        ///     Loads the card collection for the specified user from a JSON file.
        ///     If the file does not exist, returns an empty collection.
        /// </summary>
        /// <param name="userId">The user's ID.</param>
        /// <returns>
        ///     A task that returns a <see cref="UserCardCollection" /> representing the
        ///     user's saved cards.
        /// </returns>
        public static async Task<UserCardCollection> LoadUserCardsAsync(ulong userId)
        {
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory,
                $"{userId}.json");
            if (File.Exists(userFilePath))
            {
                string json = await File.ReadAllTextAsync(userFilePath);
                return JsonConvert.DeserializeObject<UserCardCollection>(json) ?? new UserCardCollection();
            }

            return new UserCardCollection { UserId = userId, Cards = new List<Card>() };
        }

        /// <summary>
        ///     Retrieves a list of random Pokémon cards from the API.
        ///     Optionally filters by set ID.
        /// </summary>
        /// <param name="count">The number of cards to retrieve.</param>
        /// <param name="setId">The set ID to filter cards by (optional).</param>
        /// <returns>
        ///     A task that returns a list of <see cref="Card" /> objects.
        /// </returns>
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
                string response = await _httpClient.GetStringAsync(requestUrl);
                var cardData = JsonConvert.DeserializeObject<ApiResponse>(response);
                return cardData?.Data?.OrderBy(_ => random.Next()).Take(count).ToList() ?? new List<Card>();
            }
            catch (Exception)
            {
                return new List<Card>();
            }
        }

        /// <summary>
        ///     Retrieves all Pokémon card sets from the API and displays them in an embed
        ///     with a selection menu.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [Command("sets")]
        public async Task GetAllSetsAsync()
        {
            try
            {
                string response = await _httpClient.GetStringAsync(SetsApiUrl);
                var setData = JsonConvert.DeserializeObject<SetApiResponse>(response);

                if (setData?.Data == null || setData.Data.Count == 0)
                {
                    await ReplyAsync("No sets found!");
                    return;
                }

                SelectMenuBuilder? selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Select a set")
                    .WithCustomId("set_selection")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (Set set in setData.Data.Take(25))
                {
                    selectMenu.AddOption(set.Name,
                        set.Id,
                        $"ID: {set.Id}");
                }

                ComponentBuilder? component = new ComponentBuilder()
                    .WithSelectMenu(selectMenu);

                Embed? embed = new EmbedBuilder()
                    .WithTitle("Pokémon Karten Sets")
                    .WithDescription(
                        "The Sets shown - This is no selection, its just for Information to retrieve the Set IDs")
                    .WithColor(Color.Green)
                    .Build();

                await ReplyAsync(embed: embed,
                    components: component.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred: " + ex.Message);
            }
        }

        /// <summary>
        ///     Handles cleanup when a user leaves the guild by deleting their saved card
        ///     collection.
        /// </summary>
        /// <param name="guild">The guild from which the user left.</param>
        /// <param name="user">The user who left.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public Task HandleUserLeft(SocketGuild guild, SocketUser user)
        {
            string userFilePath = Path.Combine(CardStorage.UserCardsDirectory,
                $"{user.Id}.json");

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

        /// <summary>
        ///     Represents a session for navigating through a list of Pokémon card sets.
        /// </summary>
        private class SetSession
        {
            /// <summary>
            ///     Gets the message ID associated with the session.
            /// </summary>
            public ulong MessageId { get; }

            /// <summary>
            ///     Gets the user ID of the participant in the session.
            /// </summary>
            public ulong UserId { get; }

            /// <summary>
            ///     Gets the list of sets in the session.
            /// </summary>
            public List<Set> Sets { get; }

            /// <summary>
            ///     Gets or sets the current index of the displayed set.
            /// </summary>
            public int CurrentIndex { get; set; }

            /// <summary>
            ///     Initializes a new instance of the <see cref="SetSession" /> class.
            /// </summary>
            /// <param name="messageId">The message ID associated with the session.</param>
            /// <param name="userId">The user ID of the session participant.</param>
            /// <param name="sets">The list of sets to navigate.</param>
            public SetSession(ulong messageId, ulong userId, List<Set> sets)
            {
                MessageId = messageId;
                UserId = userId;
                Sets = sets;
                CurrentIndex = 0;
            }
        }

        /// <summary>
        ///     Represents a session for navigating through a pack of Pokémon cards.
        /// </summary>
        public class PackSession
        {
            /// <summary>
            ///     Gets the message ID associated with the session.
            /// </summary>
            public ulong MessageId { get; }

            /// <summary>
            ///     Gets the user ID of the session participant.
            /// </summary>
            public ulong UserId { get; }

            /// <summary>
            ///     Gets the list of cards in the session.
            /// </summary>
            public List<Card> Cards { get; }

            /// <summary>
            ///     Gets or sets the current index of the displayed card.
            /// </summary>
            public int CurrentIndex { get; set; }

            /// <summary>
            ///     Initializes a new instance of the <see cref="PackSession" /> class.
            /// </summary>
            /// <param name="messageId">The message ID associated with the session.</param>
            /// <param name="userId">The user ID of the session participant.</param>
            /// <param name="cards">The list of cards to navigate.</param>
            public PackSession(ulong messageId, ulong userId, List<Card> cards)
            {
                MessageId = messageId;
                UserId = userId;
                Cards = cards;
                CurrentIndex = 0;
            }
        }

        /// <summary>
        ///     Represents the API response containing a list of Pokémon cards.
        /// </summary>
        public class ApiResponse
        {
            /// <summary>
            ///     Gets or sets the list of cards returned from the API.
            /// </summary>
            public List<Card> Data { get; set; }
        }

        /// <summary>
        ///     Represents the API response containing a list of Pokémon card sets.
        /// </summary>
        public class SetApiResponse
        {
            /// <summary>
            ///     Gets or sets the list of sets returned from the API.
            /// </summary>
            public List<Set> Data { get; set; }
        }

        /// <summary>
        ///     Represents a Pokémon card set.
        /// </summary>
        public class Set
        {
            /// <summary>
            ///     Gets or sets the name of the set.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            ///     Gets or sets the unique identifier for the set.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            ///     Gets or sets the URL for the set's image.
            /// </summary>
            public string ImageUrl { get; set; }
        }

        /// <summary>
        ///     Represents the image URLs for a Pokémon card.
        /// </summary>
        public class Images
        {
            /// <summary>
            ///     Gets or sets the URL for the small version of the card image.
            /// </summary>
            public string Small { get; set; }
        }
    }
}
