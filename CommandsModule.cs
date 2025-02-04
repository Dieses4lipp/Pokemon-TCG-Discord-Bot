using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class CommandsModule : ModuleBase<SocketCommandContext>
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string ApiUrl = "https://api.pokemontcg.io/v2/cards";

        private static readonly Dictionary<ulong, PackSession> ActiveSessions = new();

        [Command("pullcard")]
        public async Task PullCardAsync()
        {
            try
            {
                var cardData = await GetRandomCards(1);
                if (cardData.Count == 0)
                {
                    await ReplyAsync("No cards found!");
                    return;
                }

                var card = cardData[0];
                var embed = BuildCardEmbed(card, 1, 1);
                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred while retrieving the card: " + ex.Message);
            }
        }
        [Command("pullpack")]
        public async Task PullPackAsync()
        {
            try
            {
                var allCards = await GetRandomCards(100);
                if (allCards.Count == 0)
                {
                    await ReplyAsync("No cards found!");
                    return;
                }
                var random = new Random();
                var rarityChances = new Dictionary<string, double>()
                {
                    {"Common", 0.70 },
                    {"Uncommon", 0.20 },
                    {"Rare", 0.10 },
                    {"Rare Holo", 0.05 },
                    {"Ultra Rare", 0.03 },
                    {"Secret Rare", 0.01 }
                };
                var selectedCards = new List<Card>();
                for (int i = 0; i < 9; i++)
                {
                    string selectedRarity = RollRarity(random, rarityChances);
                    var possibleCards = allCards.Where(c => c.Rarity == selectedRarity).ToList();
                    if(possibleCards.Count > 0)
                    {
                        selectedCards.Add(possibleCards[random.Next(possibleCards.Count)]);
                    }
                    else
                    {
                        selectedCards.Add(allCards[random.Next(allCards.Count)]);
                    }
                }
                var embed = BuildCardEmbed(selectedCards[0], 1, selectedCards.Count);
                var message = await ReplyAsync(embed: embed);

                var session = new PackSession(message.Id, Context.User.Id, selectedCards);
                ActiveSessions[message.Id] = session;
                
                await message.AddReactionAsync(new Emoji("◀️"));
                await message.AddReactionAsync(new Emoji("▶️"));

            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred while retrieving the pack: " + ex.Message);
            }
        }
        private string RollRarity(Random random, Dictionary<string, double> rarityChances)
        {
            double roll = random.NextDouble();
            double cumulative = 0.0;
            foreach (var rarity in rarityChances)
            {
                cumulative += rarity.Value;
                if (roll <= cumulative)
                {
                    return rarity.Key;
                }
            }
            return "Common";
        }
        public async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cache, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (!ActiveSessions.ContainsKey(reaction.MessageId)) return;
            if (reaction.User.Value.IsBot) return;

            var session = ActiveSessions[reaction.MessageId];
            if (reaction.UserId != session.UserId) return;
            if(session.CurrentIndex < 0)
            {
                session.CurrentIndex = 0;
            }
            if (reaction.Emote.Name == "▶️")
                session.CurrentIndex = (session.CurrentIndex + 1) % session.Cards.Count;
            else if (reaction.Emote.Name == "◀️")
                session.CurrentIndex = (session.CurrentIndex - 1 + session.Cards.Count) % session.Cards.Count;
            else
                return;

            var message = await cache.GetOrDownloadAsync();
            await message.ModifyAsync(m => m.Embed = BuildCardEmbed(session.Cards[session.CurrentIndex], session.CurrentIndex + 1, session.Cards.Count));
            await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
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

        private async Task<List<Card>> GetRandomCards(int count)
        {
            var random = new Random();
            int pageSize = 250;
            int randomPage = random.Next(1, 50); 

            string requestUrl = $"{ApiUrl}?page={randomPage}&pageSize={pageSize}";

            var response = await _httpClient.GetStringAsync(requestUrl);
            var cardData = JsonConvert.DeserializeObject<ApiResponse>(response);

            if (cardData?.Data == null || cardData.Data.Count == 0)
                return new List<Card>();

            return cardData.Data.OrderBy(_ => random.Next()).Take(count).ToList();
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

