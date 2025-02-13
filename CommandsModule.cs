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
        private static readonly Dictionary<ulong, PackSession> ActiveSessions = new();
        private static readonly Dictionary<ulong, SetSession> ActiveSetSessions = new();


        // Constants for card rarity chances and other settings
        private static readonly Dictionary<string, double> RarityChances = new()
        {
            {"Common", 0.50 },
            {"Uncommon", 0.20 },
            {"Rare", 0.15 },
            {"Rare Holo", 0.05 },
            {"Ultra Rare", 0.07 },
            {"Secret Rare", 0.03 }
        };

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

        private static Embed BuildCardEmbed(Card card, int current, int total)
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
            if (!ActiveSessions.ContainsKey(reaction.MessageId)) return;
            if (reaction.User.Value.IsBot) return;

            var session = ActiveSessions[reaction.MessageId];
            if (reaction.UserId != session.UserId) return;

            session.CurrentIndex = (reaction.Emote.Name == "▶️")
                ? (session.CurrentIndex + 1) % session.Cards.Count
                : (session.CurrentIndex - 1 + session.Cards.Count) % session.Cards.Count;

            var message = await cache.GetOrDownloadAsync();
            await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));
            await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
        }

        private async Task<List<Card>> GetRandomCards(int count, string setId)
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

                // Liste der Set-Optionen für das Dropdown-Menü
                var selectMenu = new SelectMenuBuilder()
                    .WithPlaceholder("Wähle ein Set aus")
                    .WithCustomId("set_selection")
                    .WithMinValues(1)
                    .WithMaxValues(1);

                foreach (var set in setData.Data.Take(25)) // Discord erlaubt maximal 25 Optionen
                {
                    selectMenu.AddOption(set.Name, set.Id, $"ID: {set.Id}");
                }

                var component = new ComponentBuilder()
                    .WithSelectMenu(selectMenu);

                var embed = new EmbedBuilder()
                    .WithTitle("Pokémon Karten Sets")
                    .WithDescription("Wähle ein Set aus, um Karten zu ziehen.")
                    .WithColor(Color.Green)
                    .Build();

                await ReplyAsync(embed: embed, components: component.Build());
            }
            catch (Exception ex)
            {
                await ReplyAsync("Ein Fehler ist aufgetreten: " + ex.Message);
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


        private class PackSession
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

        public class Card
        {
            public string Name { get; set; }
            public string Rarity { get; set; }
            public Images Images { get; set; }
        }

        public class Images
        {
            public string Small { get; set; }
        }
    }
}
