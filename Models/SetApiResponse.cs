using Newtonsoft.Json;

namespace DiscordBot.Models
{
    public class SetApiResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; } = default!;

        [JsonProperty("name")]
        public string Name { get; set; } = default!;

        [JsonProperty("logo")]
        public string ImageUrl { get; set; } = default!;

        [JsonProperty("cardCount")]
        public SetCardCount CardCount { get; set; } = new();
    }

    public class SetCardCount
    {
        [JsonProperty("total")]
        public int Total { get; set; }
    }
}