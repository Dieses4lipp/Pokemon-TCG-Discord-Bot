using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Services
{

    /// <summary>
    /// Represents a collection of Pokémon cards for a specific user.
    /// </summary>
    public class UserCardCollection
    {
        public ulong UserId { get; set; }
        public List<Card> Cards { get; set; }
        public int PacksPulled { get; set; }
        public int CardsTraded { get; set; }
        public int DifferentCardsSaved => Cards.Select(c => c.Name).Distinct().Count();
        public Card FavoriteCard { get; set; }
    }
}
