using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
    using static global::DiscordBot.CommandsModule;

    /// <summary>
    /// Represents a collection of Pokémon cards for a specific user.
    /// </summary>
    public class UserCardCollection
    {
        /// <summary>
        /// Gets or sets the user ID associated with the collection.
        /// </summary>
        public ulong UserId { get; set; }

        /// <summary>
        /// Gets or sets the list of cards in the user's collection.
        /// </summary>
        public List<Card> Cards { get; set; }
    }
}
