using Discord.Commands;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class CommandsModule : ModuleBase<SocketCommandContext>
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string ApiUrl = "https://api.pokemontcg.io/v2/cards";

        private static readonly int PageSize = 10;

        [Command("pullcard")]
        public async Task PullCardAsync()
        {
            try
            {
                var random = new Random();
                int randomOffset = random.Next(0, 1000) * PageSize;
                var response = await _httpClient.GetStringAsync(ApiUrl);
                var cardData = JsonConvert.DeserializeObject<ApiResponse>(response);
                if (cardData.Data.Count == 0)
                {
                    await ReplyAsync("No cards found!");
                    return;
                }
                var random2 = new Random();
                var randomCard = cardData.Data[random2.Next(cardData.Data.Count)];
                var cardName = randomCard.Name;
                var cardRarity = randomCard.Rarity;
                var cardImage = randomCard.Images.Small;
                var embed = new Discord.EmbedBuilder()
                    .WithTitle(cardName)
                    .WithDescription($"Rarity: {cardRarity}")
                    .WithImageUrl(cardImage)
                    .Build();

                await ReplyAsync(embed: embed);
            }
            catch (Exception ex)
            {
                await ReplyAsync("An error occurred while retrieving the card: " + ex.Message);
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

